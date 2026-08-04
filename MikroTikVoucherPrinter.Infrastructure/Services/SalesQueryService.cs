using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// تنفيذ خدمة استعلام شاشة المبيعات
/// تقرأ مباشرة من قاعدة بيانات User Manager الأصلية (sqldb) أو القاعدة المحلية بالراوتر
/// المصدر الرسمي للبيع: userprofile.activated (أول استخدام حقيقي للكرت)
/// </summary>
public class SalesQueryService : ISalesQueryService
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ILogger<SalesQueryService> _logger;

    public SalesQueryService(
        IDbContextFactory<LuxCardDbContext> dbFactory,
        ILogger<SalesQueryService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  GetSalesKeysetAsync — الاستعلام الرئيسي مع Keyset Pagination
    // ══════════════════════════════════════════════════════════════════════
    public async Task<PagedResult<SalesRecordDto>> GetSalesKeysetAsync(
        SalesQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(parameters.RouterDbPath) || !File.Exists(parameters.RouterDbPath))
        {
            if (parameters.RouterId.HasValue)
            {
                return await GetSalesFromLocalDbAsync(parameters, cancellationToken);
            }
            return new PagedResult<SalesRecordDto>(Array.Empty<SalesRecordDto>(), 0, 1, parameters.PageSize);
        }

        var items = new List<SalesRecordDto>();
        int totalCount = 0;

        await Task.Run(() =>
        {
            try
            {
                var connStr = $"Data Source={parameters.RouterDbPath};Mode=ReadOnly;Cache=Shared";
                using var conn = new SqliteConnection(connStr);
                conn.Open();

                // ── 1. بناء WHERE الديناميكي ─────────────────────────────────
                var whereClauses = new List<string> { "up.activated > 0" };
                var cmd = conn.CreateCommand();

                // فلتر التاريخ: يوم واحد محدد
                if (parameters.FilterDate.HasValue)
                {
                    var start = new DateTimeOffset(parameters.FilterDate.Value.ToDateTime(TimeOnly.MinValue),
                        TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)).ToUnixTimeSeconds();
                    var end = new DateTimeOffset(parameters.FilterDate.Value.ToDateTime(TimeOnly.MaxValue),
                        TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)).ToUnixTimeSeconds();
                    whereClauses.Add("up.activated >= @dateStart AND up.activated <= @dateEnd");
                    cmd.Parameters.AddWithValue("@dateStart", start);
                    cmd.Parameters.AddWithValue("@dateEnd", end);
                }

                // فلتر الحالة
                if (!string.IsNullOrEmpty(parameters.FilterStatus))
                {
                    switch (parameters.FilterStatus.ToLower())
                    {
                        case "active":
                            whereClauses.Add("up.state != 2 AND (up.paused IS NULL OR up.paused = 0)");
                            break;
                        case "expired":
                            whereClauses.Add("up.state = 2");
                            break;
                        case "paused":
                            whereClauses.Add("up.paused = 1");
                            break;
                    }
                }

                // فلتر البحث
                if (!string.IsNullOrEmpty(parameters.SearchText?.Trim()))
                {
                    whereClauses.Add("(CAST(u.userName AS TEXT) LIKE @search OR CAST(pr.name AS TEXT) LIKE @search)");
                    cmd.Parameters.AddWithValue("@search", $"%{parameters.SearchText.Trim()}%");
                }

                // فلتر الباقة المختارة
                if (!string.IsNullOrEmpty(parameters.FilterProfile) && parameters.FilterProfile != "كل الباقات")
                {
                    whereClauses.Add("CAST(pr.name AS TEXT) = @filterProfile");
                    cmd.Parameters.AddWithValue("@filterProfile", parameters.FilterProfile);
                }

                // Keyset Pagination (cursor)
                if (parameters.AfterActivated.HasValue && parameters.AfterId.HasValue)
                {
                    whereClauses.Add("(up.activated < @afterAct OR (up.activated = @afterAct AND up.id < @afterId))");
                    cmd.Parameters.AddWithValue("@afterAct", parameters.AfterActivated.Value);
                    cmd.Parameters.AddWithValue("@afterId", parameters.AfterId.Value);
                }

                var where = string.Join(" AND ", whereClauses);

                // ── 2. استعلام العدد الإجمالي (مفلتر بالكروت الفريدة من نوعها) ──────────
                var countWhere = whereClauses.Count > 0
                    ? string.Join(" AND ", whereClauses.FindAll(w =>
                        !w.Contains("@afterAct") && !w.Contains("@afterId")))
                    : "1=1";
                if (!countWhere.Contains("up.activated > 0"))
                    countWhere = "up.activated > 0" + (countWhere.Length > 0 ? " AND " + countWhere : "");

                var countCmd = conn.CreateCommand();
                countCmd.CommandText = $@"
                    WITH LatestUserProfile AS (
                        SELECT userId, MAX(id) AS latestUpId
                        FROM userprofile
                        WHERE activated > 0
                        GROUP BY userId
                    )
                    SELECT COUNT(*)
                    FROM LatestUserProfile lup
                    JOIN userprofile up ON up.id = lup.latestUpId
                    JOIN user u ON u.id = up.userId
                    LEFT JOIN profile pr ON pr.id = up.profileId
                    WHERE {countWhere}";

                foreach (SqliteParameter p in cmd.Parameters)
                {
                    if (p.ParameterName != "@afterAct" && p.ParameterName != "@afterId")
                        countCmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                }

                totalCount = Convert.ToInt32(countCmd.ExecuteScalar());

                cmd.CommandText = $@"
                    WITH LatestUserProfile AS (
                        SELECT userId, MAX(id) AS latestUpId
                        FROM userprofile
                        WHERE activated > 0
                        GROUP BY userId
                    )
                    SELECT 
                        up.id,
                        up.activated,
                        up.price,
                        up.state,
                        up.paused,
                        CAST(u.userName AS TEXT)    AS userName,
                        u.lastSeenAt,
                        u.uptimeUsed,
                        u.downloadUsed,
                        u.uploadUsed,
                        CAST(pr.name AS TEXT)       AS profileName,
                        MAX(COALESCE(lim.uptimeLimit, 0))   AS uptimeLimit,
                        MAX(COALESCE(lim.downloadLimit, 0)) AS downloadLimit,
                        MAX(COALESCE(lim.uploadLimit, 0))   AS uploadLimit,
                        MAX(COALESCE(lim.transferLimit, 0)) AS transferLimit
                    FROM LatestUserProfile lup
                    JOIN userprofile up ON up.id = lup.latestUpId
                    JOIN user u ON u.id = up.userId
                    LEFT JOIN profile pr ON pr.id = up.profileId
                    LEFT JOIN pparts pp ON pp.profileId = pr.id
                    LEFT JOIN limitation lim ON lim.id = pp.limitId
                    WHERE {where}
                    GROUP BY up.id
                    ORDER BY up.activated DESC, up.id DESC
                    LIMIT @limit";

                cmd.Parameters.AddWithValue("@limit", parameters.PageSize);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    items.Add(MapRow(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [SalesQueryService] Error executing sales query on SQLite DB: {DbPath}", parameters.RouterDbPath);
                SqliteConnection.ClearAllPools();
            }

        }, cancellationToken);

        return new PagedResult<SalesRecordDto>(items, totalCount, 1, parameters.PageSize);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  GetSalesKpiAsync — حساب بطاقات KPI
    // ══════════════════════════════════════════════════════════════════════
    public async Task<SalesKpiDto> GetSalesKpiAsync(
        SalesQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var kpi = new SalesKpiDto();

        if (string.IsNullOrEmpty(parameters.RouterDbPath) || !File.Exists(parameters.RouterDbPath))
        {
            if (parameters.RouterId.HasValue)
            {
                return await GetSalesKpiFromLocalDbAsync(parameters, cancellationToken);
            }
            return kpi;
        }

        await Task.Run(() =>
        {
            try
            {
                var connStr = $"Data Source={parameters.RouterDbPath};Mode=ReadOnly;Cache=Shared";
                using var conn = new SqliteConnection(connStr);
                conn.Open();

                // حساب حدود الفترات الزمنية بالتوقيت المحلي
                var now = DateTime.Now;
                var todayStart  = new DateTimeOffset(now.Date, TimeZoneInfo.Local.GetUtcOffset(now)).ToUnixTimeSeconds();
                var yesterdayStart = todayStart - 86400;
                var weekStart   = todayStart - (7 * 86400);
                var monthStart  = new DateTimeOffset(new DateTime(now.Year, now.Month, 1),
                    TimeZoneInfo.Local.GetUtcOffset(now)).ToUnixTimeSeconds();

                var whereClauses = new List<string> { "up.activated > 0" };
                if (!string.IsNullOrEmpty(parameters.FilterProfile) && parameters.FilterProfile != "كل الباقات")
                {
                    whereClauses.Add("CAST(pr.name AS TEXT) = @filterProfile");
                }
                var where = string.Join(" AND ", whereClauses);

                var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    WITH LatestUserProfile AS (
                        SELECT userId, MAX(id) AS latestUpId
                        FROM userprofile
                        WHERE activated > 0
                        GROUP BY userId
                    )
                    SELECT
                        SUM(CASE WHEN up.activated >= @todayStart THEN 1 ELSE 0 END)     AS todaySales,
                        SUM(CASE WHEN up.activated >= @yesterdayStart AND up.activated < @todayStart THEN 1 ELSE 0 END) AS yesterdaySales,
                        SUM(CASE WHEN up.activated >= @weekStart THEN 1 ELSE 0 END)      AS weeklySales,
                        SUM(CASE WHEN up.activated >= @monthStart THEN 1 ELSE 0 END)     AS monthlySales,
                        COUNT(*)                                                        AS totalSales,

                        SUM(CASE WHEN up.activated >= @todayStart THEN COALESCE(up.price, 0) ELSE 0 END)     AS todayRevenue,
                        SUM(CASE WHEN up.activated >= @yesterdayStart AND up.activated < @todayStart THEN COALESCE(up.price, 0) ELSE 0 END) AS yesterdayRevenue,
                        SUM(CASE WHEN up.activated >= @weekStart THEN COALESCE(up.price, 0) ELSE 0 END)      AS weeklyRevenue,
                        SUM(CASE WHEN up.activated >= @monthStart THEN COALESCE(up.price, 0) ELSE 0 END)     AS monthlyRevenue,
                        SUM(COALESCE(up.price, 0))                                                           AS totalRevenue
                    FROM LatestUserProfile lup
                    JOIN userprofile up ON up.id = lup.latestUpId
                    LEFT JOIN profile pr ON pr.id = up.profileId
                    WHERE {where}";

                cmd.Parameters.AddWithValue("@todayStart",     todayStart);
                cmd.Parameters.AddWithValue("@yesterdayStart", yesterdayStart);
                cmd.Parameters.AddWithValue("@weekStart",      weekStart);
                cmd.Parameters.AddWithValue("@monthStart",     monthStart);
                if (!string.IsNullOrEmpty(parameters.FilterProfile) && parameters.FilterProfile != "كل الباقات")
                {
                    cmd.Parameters.AddWithValue("@filterProfile", parameters.FilterProfile);
                }

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    kpi.TodaySales       = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    kpi.YesterdaySales   = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    kpi.WeeklySales      = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    kpi.MonthlySales     = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    kpi.TotalSales       = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);

                    kpi.TodayRevenue     = reader.IsDBNull(5) ? 0L : reader.GetInt64(5) / 100;
                    kpi.YesterdayRevenue = reader.IsDBNull(6) ? 0L : reader.GetInt64(6) / 100;
                    kpi.WeeklyRevenue    = reader.IsDBNull(7) ? 0L : reader.GetInt64(7) / 100;
                    kpi.MonthlyRevenue   = reader.IsDBNull(8) ? 0L : reader.GetInt64(8) / 100;
                    kpi.TotalRevenue     = reader.IsDBNull(9) ? 0L : reader.GetInt64(9) / 100;
                }

                // الكروت غير المستخدمة (activated = 0)
                var unusedCmd = conn.CreateCommand();
                unusedCmd.CommandText = $@"
                    SELECT COUNT(*) 
                    FROM userprofile up
                    LEFT JOIN profile pr ON pr.id = up.profileId
                    WHERE up.activated = 0 AND {(string.IsNullOrEmpty(parameters.FilterProfile) || parameters.FilterProfile == "كل الباقات" ? "1=1" : "CAST(pr.name AS TEXT) = @filterProfile")}";
                if (!string.IsNullOrEmpty(parameters.FilterProfile) && parameters.FilterProfile != "كل الباقات")
                {
                    unusedCmd.Parameters.AddWithValue("@filterProfile", parameters.FilterProfile);
                }
                kpi.UnusedInventory = Convert.ToInt32(unusedCmd.ExecuteScalar());

                // حساب الباقة الأكثر مبيعاً اليوم
                var todayBestCmd = conn.CreateCommand();
                todayBestCmd.CommandText = @"
                    WITH LatestUserProfile AS (
                        SELECT userId, MAX(id) AS latestUpId
                        FROM userprofile
                        WHERE activated > 0
                        GROUP BY userId
                    )
                    SELECT CAST(pr.name AS TEXT) as pname, COUNT(*) as cnt
                    FROM LatestUserProfile lup
                    JOIN userprofile up ON up.id = lup.latestUpId
                    JOIN profile pr ON pr.id = up.profileId
                    WHERE up.activated >= @todayStart
                    GROUP BY pname
                    ORDER BY cnt DESC
                    LIMIT 1";
                todayBestCmd.Parameters.AddWithValue("@todayStart", todayStart);
                var todayBestObj = todayBestCmd.ExecuteScalar();
                kpi.TodayBestProfile = todayBestObj != null ? todayBestObj.ToString() : "لا يوجد";

                // حساب الباقة الأكثر مبيعاً أمس
                var yesterdayBestCmd = conn.CreateCommand();
                yesterdayBestCmd.CommandText = @"
                    WITH LatestUserProfile AS (
                        SELECT userId, MAX(id) AS latestUpId
                        FROM userprofile
                        WHERE activated > 0
                        GROUP BY userId
                    )
                    SELECT CAST(pr.name AS TEXT) as pname, COUNT(*) as cnt
                    FROM LatestUserProfile lup
                    JOIN userprofile up ON up.id = lup.latestUpId
                    JOIN profile pr ON pr.id = up.profileId
                    WHERE up.activated >= @yesterdayStart AND up.activated < @todayStart
                    GROUP BY pname
                    ORDER BY cnt DESC
                    LIMIT 1";
                yesterdayBestCmd.Parameters.AddWithValue("@yesterdayStart", yesterdayStart);
                yesterdayBestCmd.Parameters.AddWithValue("@todayStart", todayStart);
                var yesterdayBestObj = yesterdayBestCmd.ExecuteScalar();
                kpi.YesterdayBestProfile = yesterdayBestObj != null ? yesterdayBestObj.ToString() : "لا يوجد";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [SalesQueryService] Error executing sales KPI query on SQLite DB: {DbPath}", parameters.RouterDbPath);
                SqliteConnection.ClearAllPools();
            }

        }, cancellationToken);

        return kpi;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  GetSalesCountAsync
    // ══════════════════════════════════════════════════════════════════════
    public async Task<int> GetSalesCountAsync(
        SalesQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await GetSalesKeysetAsync(parameters with { PageSize = 1 }, cancellationToken);
        return result.TotalCount;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  MapRow — تحويل DataReader → SalesRecordDto
    // ══════════════════════════════════════════════════════════════════════
    private static SalesRecordDto MapRow(SqliteDataReader r)
    {
        return new SalesRecordDto
        {
            Id               = r.IsDBNull(0) ? 0  : r.GetInt32(0),
            ActivatedUnix    = r.IsDBNull(1) ? 0L : r.GetInt64(1),
            PriceRaw         = r.IsDBNull(2) ? 0L : r.GetInt64(2),
            State            = r.IsDBNull(3) ? 0  : r.GetInt32(3),
            IsPaused         = !r.IsDBNull(4) && r.GetInt32(4) == 1,
            VoucherCode      = r.IsDBNull(5) ? "—" : r.GetString(5),
            LastSeenAtUnix   = r.IsDBNull(6) ? 0L : r.GetInt64(6),
            UptimeUsedSeconds= r.IsDBNull(7) ? 0L : r.GetInt64(7),
            DownloadUsedBytes= r.IsDBNull(8) ? 0L : r.GetInt64(8),
            UploadUsedBytes  = r.IsDBNull(9) ? 0L : r.GetInt64(9),
            ProfileName      = r.IsDBNull(10)? "—": r.GetString(10),
            UptimeLimit      = r.FieldCount > 11 && !r.IsDBNull(11) ? r.GetInt64(11) : 0L,
            DownloadLimit    = r.FieldCount > 12 && !r.IsDBNull(12) ? r.GetInt64(12) : 0L,
            UploadLimit      = r.FieldCount > 13 && !r.IsDBNull(13) ? r.GetInt64(13) : 0L,
            TransferLimit    = r.FieldCount > 14 && !r.IsDBNull(14) ? r.GetInt64(14) : 0L,
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    //  GetProfilesAsync — جلب الباقات المتاحة
    // ══════════════════════════════════════════════════════════════════════
    public async Task<List<string>> GetProfilesAsync(
        string routerDbPath,
        CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(routerDbPath) || !File.Exists(routerDbPath))
            return list;

        await Task.Run(() =>
        {
            var connStr = $"Data Source={routerDbPath};Mode=ReadOnly;Cache=Shared";
            using var conn = new SqliteConnection(connStr);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT CAST(name AS TEXT) FROM profile WHERE name <> '' ORDER BY name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetString(0));
            }
        }, cancellationToken);

        return list;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  الجلب الاحتياطي من قاعدة بيانات الكروت المحلية (للراوترات بدون User Manager)
    // ══════════════════════════════════════════════════════════════════════
    private async Task<PagedResult<SalesRecordDto>> GetSalesFromLocalDbAsync(
        SalesQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Vouchers
            .IgnoreQueryFilters()
            .Where(v => v.RouterId == parameters.RouterId!.Value && !v.IsDeleted);

        // فلتر الحالة
        if (!string.IsNullOrEmpty(parameters.FilterStatus))
        {
            switch (parameters.FilterStatus.ToLower())
            {
                case "active":
                    query = query.Where(v => v.Status == Domain.Enums.VoucherStatus.Used);
                    break;
                case "expired":
                    query = query.Where(v => v.Status == Domain.Enums.VoucherStatus.Expired);
                    break;
            }
        }

        // فلتر الباقة
        if (!string.IsNullOrEmpty(parameters.FilterProfile) && parameters.FilterProfile != "كل الباقات")
        {
            query = query.Where(v => v.ProfileName == parameters.FilterProfile);
        }

        // فلتر البحث
        if (!string.IsNullOrWhiteSpace(parameters.SearchText))
        {
            var search = parameters.SearchText.Trim();
            query = query.Where(v => v.Username.Contains(search) || v.ProfileName.Contains(search));
        }

        // فلتر التاريخ
        if (parameters.FilterDate.HasValue)
        {
            var startUtc = parameters.FilterDate.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            var endUtc = parameters.FilterDate.Value.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();
            query = query.Where(v => (v.SyncedAt ?? v.CreatedAt) >= startUtc && (v.SyncedAt ?? v.CreatedAt) <= endUtc);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var list = await query
            .OrderByDescending(v => v.SyncedAt ?? v.CreatedAt)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = list.Select((v, index) => new SalesRecordDto
        {
            Id = index + 1,
            ActivatedUnix = new DateTimeOffset(v.SyncedAt ?? v.CreatedAt).ToUnixTimeSeconds(),
            PriceRaw = (long)(v.Price * 100),
            State = v.Status == Domain.Enums.VoucherStatus.Expired ? 2 : 1,
            IsPaused = v.IsDisabled,
            VoucherCode = v.Username,
            LastSeenAtUnix = new DateTimeOffset(v.SyncedAt ?? v.CreatedAt).ToUnixTimeSeconds(),
            UptimeUsedSeconds = v.UptimeUsedSeconds,
            DownloadUsedBytes = v.DownloadUsedBytes,
            UploadUsedBytes = v.UploadUsedBytes,
            ProfileName = v.ProfileName
        }).ToList();

        return new PagedResult<SalesRecordDto>(dtos, totalCount, 1, parameters.PageSize);
    }

    private async Task<SalesKpiDto> GetSalesKpiFromLocalDbAsync(
        SalesQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var kpi = new SalesKpiDto();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Vouchers
            .IgnoreQueryFilters()
            .Where(v => v.RouterId == parameters.RouterId!.Value && !v.IsDeleted);

        if (!string.IsNullOrEmpty(parameters.FilterProfile) && parameters.FilterProfile != "كل الباقات")
        {
            query = query.Where(v => v.ProfileName == parameters.FilterProfile);
        }

        var vouchers = await query.ToListAsync(cancellationToken);

        var todayUtc = DateTime.UtcNow.Date;
        var yesterdayUtc = todayUtc.AddDays(-1);
        var weekUtc = todayUtc.AddDays(-7);
        var monthUtc = new DateTime(todayUtc.Year, todayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        kpi.TodaySales = vouchers.Count(v => (v.SyncedAt ?? v.CreatedAt).Date >= todayUtc);
        kpi.YesterdaySales = vouchers.Count(v => (v.SyncedAt ?? v.CreatedAt).Date >= yesterdayUtc && (v.SyncedAt ?? v.CreatedAt).Date < todayUtc);
        kpi.WeeklySales = vouchers.Count(v => (v.SyncedAt ?? v.CreatedAt).Date >= weekUtc);
        kpi.MonthlySales = vouchers.Count(v => (v.SyncedAt ?? v.CreatedAt).Date >= monthUtc);
        kpi.TotalSales = vouchers.Count;

        kpi.TodayRevenue = (long)vouchers.Where(v => (v.SyncedAt ?? v.CreatedAt).Date >= todayUtc).Sum(v => v.Price);
        kpi.YesterdayRevenue = (long)vouchers.Where(v => (v.SyncedAt ?? v.CreatedAt).Date >= yesterdayUtc && (v.SyncedAt ?? v.CreatedAt).Date < todayUtc).Sum(v => v.Price);
        kpi.WeeklyRevenue = (long)vouchers.Where(v => (v.SyncedAt ?? v.CreatedAt).Date >= weekUtc).Sum(v => v.Price);
        kpi.MonthlyRevenue = (long)vouchers.Where(v => (v.SyncedAt ?? v.CreatedAt).Date >= monthUtc).Sum(v => v.Price);
        kpi.TotalRevenue = (long)vouchers.Sum(v => v.Price);

        kpi.UnusedInventory = vouchers.Count(v => v.Status == Domain.Enums.VoucherStatus.Unused);

        return kpi;
    }
}
