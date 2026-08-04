using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// يُشغَّل مرة واحدة عند أول تشغيل بعد التحديث.
/// يجمع Vouchers اليتيمة (بدون BatchId صالح) في Legacy Batches
/// حسب: ProfileName + تاريخ الإنشاء.
/// لا يبقى أي Voucher بدون BatchId بعد هذه العملية.
/// </summary>
public class BatchMigrationService
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ILogger<BatchMigrationService>      _logger;

    public BatchMigrationService(
        IDbContextFactory<LuxCardDbContext> dbFactory,
        ILogger<BatchMigrationService> logger)
    {
        _dbFactory = dbFactory;
        _logger    = logger;
    }

    /// <summary>
    /// يتحقق هل هناك Vouchers يتيمة ويُهاجرها إذا وُجدت.
    /// يُستدعى من Startup — آمن للاستدعاء المتكرر.
    /// </summary>
    public async Task MigrateIfNeededAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // جلب معرفات الدفعات الموجودة فعلاً
        var existingBatchIds = (await db.Batches
            .IgnoreQueryFilters()
            .Select(b => b.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        // الكروت بدون BatchId صالح أو بـ BatchId غير موجود
        var orphans = await db.Vouchers
            .IgnoreQueryFilters()
            .Where(v => !v.IsDeleted &&
                        (v.BatchId == Guid.Empty || !existingBatchIds.Contains(v.BatchId)))
            .ToListAsync(cancellationToken);

        if (!orphans.Any())
        {
            _logger.LogInformation("✅ [Migration] لا توجد كروت يتيمة — قاعدة البيانات سليمة.");
            return;
        }

        _logger.LogWarning("⚠️ [Migration] وُجد {Count} كرت يتيم — بدء التجميع في Legacy Batches...", orphans.Count);

        // تجميع حسب: ProfileName + تاريخ الإنشاء (يوم)
        var groups = orphans
            .GroupBy(v => new
            {
                Profile = string.IsNullOrWhiteSpace(v.ProfileName) ? "Unknown" : v.ProfileName,
                Day     = v.CreatedAt.Date
            });

        int batchesCreated = 0;
        int vouchersFixed  = 0;

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var routerId = group.First().RouterId;
            var batchName = $"دفعة تراثية - {group.Key.Profile} - {group.Key.Day:yyyy-MM-dd}";

            var legacyBatch = new Batch
            {
                Id           = Guid.NewGuid(),
                Name         = batchName,
                Description  = "دفعة أُنشئت تلقائياً أثناء هجرة البيانات.",
                CreatedBy    = "Migration Service",
                ProfileName  = group.Key.Profile,
                RouterId     = routerId,
                TotalCards   = group.Count(),
                GeneratedCards = group.Count(),
                Status       = BatchStatus.Completed,
                SyncStatus   = BatchSyncStatus.Completed,
                CreatedAt    = group.Key.Day.AddHours(12),
                Metadata     = """{"source":"auto-migration","version":"1.0"}"""
            };

            // حساب الكروت المزامنة
            legacyBatch.SyncedCards = group.Count(v => v.SyncStatus == SyncStatus.Synced);
            legacyBatch.FailedCards = group.Count(v => v.SyncStatus == SyncStatus.Failed);

            db.Batches.Add(legacyBatch);

            foreach (var voucher in group)
            {
                voucher.BatchId   = legacyBatch.Id;
                voucher.UpdatedAt = DateTime.UtcNow;
            }

            batchesCreated++;
            vouchersFixed += group.Count();
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "✅ [Migration] اكتملت الهجرة: {Batches} دفعة تراثية أُنشئت، {Vouchers} كرت صُنّف.",
            batchesCreated, vouchersFixed);
    }
}
