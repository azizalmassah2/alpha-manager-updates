using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class VlanTelemetryService : IVlanTelemetryService
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ILogger<VlanTelemetryService> _logger;

    public VlanTelemetryService(
        IDbContextFactory<LuxCardDbContext> dbFactory,
        ILogger<VlanTelemetryService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task ProcessVlanSamplesAsync(
        Guid routerId,
        IEnumerable<(string VlanName, long CurrentRx, long CurrentTx)> vlanSamples,
        CancellationToken cancellationToken = default)
    {
        if (routerId == Guid.Empty || vlanSamples == null) return;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var existingStates = await db.VlanTelemetryStates
                .IgnoreQueryFilters()
                .Where(s => s.RouterId == routerId && !s.IsDeleted)
                .ToDictionaryAsync(s => s.VlanName, StringComparer.OrdinalIgnoreCase, cancellationToken);

            bool modified = false;
            var now = DateTime.UtcNow;

            foreach (var (vlanName, currentRx, currentTx) in vlanSamples)
            {
                if (string.IsNullOrWhiteSpace(vlanName)) continue;

                if (!existingStates.TryGetValue(vlanName, out var state))
                {
                    state = new VlanTelemetryState
                    {
                        Id = Guid.NewGuid(),
                        RouterId = routerId,
                        VlanName = vlanName,
                        CumulativeRxBytes = 0,
                        CumulativeTxBytes = 0,
                        LastRawRxBytes = currentRx,
                        LastRawTxBytes = currentTx,
                        LastSampleTime = now,
                        RebootCount = 0
                    };
                    db.VlanTelemetryStates.Add(state);
                    existingStates[vlanName] = state;
                    modified = true;
                    continue;
                }

                // 1. فحص كشف إعادة التشغيل (Reboot Detection)
                bool isRxReset = state.LastRawRxBytes > 0 && currentRx < state.LastRawRxBytes;
                bool isTxReset = state.LastRawTxBytes > 0 && currentTx < state.LastRawTxBytes;

                if (isRxReset || isTxReset)
                {
                    state.RebootCount++;
                    _logger.LogInformation("🔄 تم اكتشاف إعادة تشغيل للمايكروتك أو تصفير العداد للفيلان {VlanName}. القراءة السابقة: Rx={LastRx}, القراءة الحالية: Rx={CurrentRx}",
                        vlanName, state.LastRawRxBytes, currentRx);

                    // إضافة الاستهلاك السابق قبل التصفير للإجمالي التراكمي
                    if (isRxReset)
                    {
                        state.CumulativeRxBytes += state.LastRawRxBytes;
                    }
                    if (isTxReset)
                    {
                        state.CumulativeTxBytes += state.LastRawTxBytes;
                    }

                    // 2. فحص محرك سد الفجوات من يوزر مانجر (User Manager Gap Filler) إذا كان هناك فارق زمني
                    var offlineDuration = now - state.LastSampleTime;
                    if (offlineDuration.TotalMinutes > 3)
                    {
                        var gapUsage = await EstimateUserManagerGapUsageAsync(db, routerId, vlanName, state.LastSampleTime, now, cancellationToken);
                        if (gapUsage.Rx > 0 || gapUsage.Tx > 0)
                        {
                            state.CumulativeRxBytes += gapUsage.Rx;
                            state.CumulativeTxBytes += gapUsage.Tx;
                            _logger.LogInformation("⭐ تم سد الفجوة الزمنية للفيلان {VlanName} عبر يوزر مانجر بمقدار: Rx={GapRx}, Tx={GapTx}",
                                vlanName, gapUsage.Rx, gapUsage.Tx);
                        }
                    }
                }
                else
                {
                    // نمو عادي للعداد بدون تصفير
                    // نراكم أي زيادة إذا لزم الأمر
                }

                state.LastRawRxBytes = currentRx;
                state.LastRawTxBytes = currentTx;
                state.LastSampleTime = now;
                modified = true;
            }

            if (modified)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء معالجة عينات استهلاك الفيلانات للراوتر {RouterId}", routerId);
        }
    }

    public async Task<Dictionary<string, (long TotalRx, long TotalTx)>> GetVlanCumulativeTotalsAsync(
        Guid routerId,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, (long TotalRx, long TotalTx)>(StringComparer.OrdinalIgnoreCase);
        if (routerId == Guid.Empty) return result;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var states = await db.VlanTelemetryStates
                .IgnoreQueryFilters()
                .Where(s => s.RouterId == routerId && !s.IsDeleted)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var s in states)
            {
                long totalRx = s.CumulativeRxBytes + s.LastRawRxBytes;
                long totalTx = s.CumulativeTxBytes + s.LastRawTxBytes;
                result[s.VlanName] = (totalRx, totalTx);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء جلب إحصائيات الفيلانات التراكمية للراوتر {RouterId}", routerId);
        }

        return result;
    }

    public async Task<Application.DTOs.VlanAnalyticsReportDto> GetVlanAnalyticsReportAsync(
        Guid routerId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var report = new Application.DTOs.VlanAnalyticsReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            PeriodTitle = $"{fromDate:yyyy-MM-dd} ── {toDate:yyyy-MM-dd}"
        };

        if (routerId == Guid.Empty) return report;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var states = await db.VlanTelemetryStates
                .IgnoreQueryFilters()
                .Where(s => s.RouterId == routerId && !s.IsDeleted)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            long grandTotal = 0;
            var list = new List<Application.DTOs.VlanReportDto>();

            foreach (var s in states)
            {
                long dl = s.CumulativeTxBytes + s.LastRawTxBytes;
                long ul = s.CumulativeRxBytes + s.LastRawRxBytes;
                long total = dl + ul;
                grandTotal += total;

                list.Add(new Application.DTOs.VlanReportDto
                {
                    VlanName = s.VlanName,
                    DisplayName = s.VlanName,
                    DownloadBytes = dl,
                    UploadBytes = ul,
                    LastActiveTime = s.LastSampleTime,
                    HealthStatus = "🟢 نشط"
                });
            }

            report.GrandTotalBytes = grandTotal;

            // حساب النسب التنافسية للشبكة والترتيب
            var ordered = list.OrderByDescending(x => x.TotalBytes).ToList();
            int rank = 1;
            foreach (var item in ordered)
            {
                item.Rank = rank++;
                item.NetworkSharePercent = grandTotal > 0 ? (item.TotalBytes * 100.0 / grandTotal) : 0;
            }

            report.VlanItems = ordered;
            report.TopUsageVlan = ordered.FirstOrDefault();
            report.LeastUsageVlan = ordered.LastOrDefault(x => x.TotalBytes > 0) ?? ordered.LastOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء استخراج تقرير تحليلات الفيلانات للراوتر {RouterId}", routerId);
        }

        return report;
    }

    private async Task<(long Rx, long Tx)> EstimateUserManagerGapUsageAsync(
        LuxCardDbContext db,
        Guid routerId,
        string vlanName,
        DateTime fromTime,
        DateTime toTime,
        CancellationToken cancellationToken)
    {
        try
        {
            // استعلام خفيف لسحب مجموع الاستهلاك المسجل في الكروت المحدثة بين التوقيتين
            var vouchers = await db.Vouchers
                .IgnoreQueryFilters()
                .Where(v => v.RouterId == routerId && v.UpdatedAt >= fromTime && v.UpdatedAt <= toTime)
                .Select(v => new { v.DownloadUsedBytes, v.UploadUsedBytes })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            long totalDownload = vouchers.Sum(v => v.DownloadUsedBytes);
            long totalUpload = vouchers.Sum(v => v.UploadUsedBytes);

            return (totalDownload, totalUpload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذر تقدير فجوة يوزر مانجر للفيلان {VlanName}", vlanName);
            return (0, 0);
        }
    }
}
