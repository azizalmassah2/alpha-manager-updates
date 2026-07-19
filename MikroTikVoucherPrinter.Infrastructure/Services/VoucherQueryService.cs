using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;
using tik4net;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class VoucherQueryService : IVoucherQueryService
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ISettingsService _settingsService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly ISecureStorageService _secureStorageService;

    public VoucherQueryService(
        IDbContextFactory<LuxCardDbContext> dbFactory, 
        ISettingsService settingsService,
        IActiveRouterContext activeRouterContext,
        ISecureStorageService secureStorageService)
    {
        _dbFactory = dbFactory;
        _settingsService = settingsService;
        _activeRouterContext = activeRouterContext;
        _secureStorageService = secureStorageService;
    }

    public async Task<IReadOnlyList<VoucherDto>> GetVouchersByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vouchers
            .Include(v => v.Agent)
            .AsNoTracking()
            .Where(v => v.BatchId == batchId)
            .Select(v => new VoucherDto
            {
                Id             = v.Id,
                Username       = v.Username,
                Password       = v.Password,
                Profile        = v.ProfileName,
                Price          = v.Price,
                Status         = v.Status,
                SyncStatus     = v.SyncStatus,
                CreatedAt      = v.CreatedAt,
                BatchId        = v.BatchId,
                CredentialMode = v.CredentialMode,
                DataOrigin     = VoucherDataOrigin.Local,
                AgentName      = v.Agent != null ? v.Agent.Name : "-",
                DownloadUsedBytes = v.DownloadUsedBytes,
                UploadUsedBytes = v.UploadUsedBytes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VoucherDto>> GetPendingSyncVouchersProjectedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vouchers
            .Include(v => v.Agent)
            .AsNoTracking()
            .Where(v => v.SyncStatus == SyncStatus.Pending)
            .Select(v => new VoucherDto
            {
                Id             = v.Id,
                Username       = v.Username,
                Password       = v.Password,
                Profile        = v.ProfileName,
                Price          = v.Price,
                Status         = v.Status,
                SyncStatus     = v.SyncStatus,
                CreatedAt      = v.CreatedAt,
                BatchId        = v.BatchId,
                CredentialMode = v.CredentialMode,
                DataOrigin     = VoucherDataOrigin.Local,
                AgentName      = v.Agent != null ? v.Agent.Name : "-",
                DownloadUsedBytes = v.DownloadUsedBytes,
                UploadUsedBytes = v.UploadUsedBytes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var routerId = _activeRouterContext.CurrentRouterId;
        if (routerId == null || routerId == Guid.Empty)
        {
            return new DashboardStatsDto();
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var today = DateTime.Today;
        var stats = new DashboardStatsDto();

        // 1. Single database GROUP BY query to fetch count aggregates efficiently
        var counts = await db.Vouchers
            .IgnoreQueryFilters()
            .Where(v => v.RouterId == routerId.Value)
            .GroupBy(v => new { v.SyncStatus, v.Status, v.IsDeleted })
            .Select(g => new
            {
                g.Key.SyncStatus,
                g.Key.Status,
                g.Key.IsDeleted,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Map computed aggregates from in-memory results
        stats.TotalVouchers   = counts.Where(c => !c.IsDeleted).Sum(c => c.Count);
        stats.SyncedVouchers  = counts.Where(c => !c.IsDeleted && c.SyncStatus == SyncStatus.Synced).Sum(c => c.Count);
        stats.PendingVouchers = counts.Where(c => !c.IsDeleted && c.SyncStatus == SyncStatus.Pending).Sum(c => c.Count);
        stats.FailedVouchers  = counts.Where(c => !c.IsDeleted && c.SyncStatus == SyncStatus.Failed).Sum(c => c.Count);

        stats.UsedVouchers    = counts.Where(c => !c.IsDeleted && c.Status == VoucherStatus.Used).Sum(c => c.Count);
        stats.ExpiredVouchers = counts.Where(c => !c.IsDeleted && c.Status == VoucherStatus.Expired).Sum(c => c.Count);

        // 2. Fetch today's sum of voucher prices in a single query
        stats.TodaySales = await db.Vouchers
            .IgnoreQueryFilters()
            .Where(v => v.RouterId == routerId.Value && !v.IsDeleted && v.CreatedAt >= today)
            .Select(v => v.Price)
            .SumAsync(cancellationToken);

        return stats;
    }

    [Obsolete("Use GetVouchersKeysetAsync instead for paginated loading.", false)]
    public async Task<IReadOnlyList<VoucherDto>> GetAllVouchersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vouchers
            .IgnoreQueryFilters()
            .Include(v => v.Agent)
            .AsNoTracking()
            .Where(v => v.RouterId == _activeRouterContext.CurrentRouterId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VoucherDto
            {
                Id             = v.Id,
                Username       = v.Username,
                Password       = v.Password,
                Profile        = v.ProfileName,
                Price          = v.Price,
                Status         = v.Status,
                SyncStatus     = v.SyncStatus,
                CreatedAt      = v.CreatedAt,
                BatchId        = v.BatchId,
                CredentialMode = v.CredentialMode,
                DataOrigin     = VoucherDataOrigin.Local,
                AgentName      = v.Agent != null ? v.Agent.Name : "-",
                IsDeleted      = v.IsDeleted,
                DownloadUsedBytes = v.DownloadUsedBytes,
                UploadUsedBytes = v.UploadUsedBytes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VoucherDto>> GetAllVouchersFromMikroTikAsync(CancellationToken cancellationToken = default)
    {


        var activeRouter = _activeRouterContext.CurrentRouter;
        if (activeRouter == null)
            throw new InvalidOperationException("لا يوجد راوتر نشط حالياً.");

        var host = activeRouter.Host;
        var user = activeRouter.Username;
        var pass = "";
        if (!string.IsNullOrEmpty(activeRouter.EncryptedPassword))
        {
            try
            {
                pass = _secureStorageService.Decrypt(activeRouter.EncryptedPassword);
            }
            catch (Exception ex)
            {
                
            }
        }

        Dictionary<string, Voucher> localLookup;
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var list = await db.Vouchers
                .IgnoreQueryFilters()
                .Include(v => v.Agent)
                .AsNoTracking()
                .Where(v => v.RouterId == _activeRouterContext.CurrentRouterId)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync(cancellationToken);

            localLookup = list.GroupBy(v => v.Username)
                .Select(g => g.First())
                .ToDictionary(v => v.Username, StringComparer.OrdinalIgnoreCase);
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
            connection.SendTimeout = 10000;
            connection.ReceiveTimeout = 10000;
            connection.Open(host, user, pass);

            IEnumerable<ITikSentence> users;
            var isHotspot = false;

            try
            {
                users = connection.CreateCommandAndParameters("/tool/user-manager/user/print").ExecuteList();
            }
            catch
            {
                try
                {
                    users = connection.CreateCommandAndParameters("/user-manager/user/print").ExecuteList();
                }
                catch
                {
                    users = connection.CreateCommandAndParameters("/ip/hotspot/user/print").ExecuteList();
                    isHotspot = true;
                }
            }

            var result = new List<VoucherDto>();
            foreach (var sentence in users)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var username = GetWord(sentence, isHotspot ? "name" : "username");
                if (string.IsNullOrWhiteSpace(username))
                    continue;

                var password = GetWord(sentence, "password");
                var profile = GetWord(sentence, "profile");
                if (string.IsNullOrWhiteSpace(profile))
                    profile = GetWord(sentence, "actual-profile");

                var localFound = localLookup.TryGetValue(username, out var localVoucher);

                ApplyQuotaFromSentence(sentence, isHotspot, out var used, out var limit, out var depleted);
                var expiredLocal = InferExpiredFromSentence(sentence, isHotspot);

                long? downloadUsed = null;
                long? uploadUsed = null;
                if (isHotspot)
                {
                    downloadUsed = TryLong(GetWord(sentence, "bytes-out"));
                    uploadUsed = TryLong(GetWord(sentence, "bytes-in"));
                }
                else
                {
                    downloadUsed = TryLong(GetWord(sentence, "download-used"));
                    uploadUsed = TryLong(GetWord(sentence, "upload-used"));
                }

                var calculatedStatus = VoucherStatus.Unused;
                if (isHotspot)
                {
                    calculatedStatus = expiredLocal ? VoucherStatus.Expired : (localFound ? localVoucher!.Status : VoucherStatus.Unused);
                }
                else
                {
                    var lastSeen = GetWord(sentence, "last-seen");
                    var uptimeSecs = GetWord(sentence, "uptime-used");
                    var hasUsage = (downloadUsed.HasValue && downloadUsed.Value > 0) ||
                                   (uploadUsed.HasValue && uploadUsed.Value > 0) ||
                                   (!string.IsNullOrEmpty(uptimeSecs) && uptimeSecs != "0s") ||
                                   (!string.IsNullOrEmpty(lastSeen) && !string.Equals(lastSeen, "never", StringComparison.OrdinalIgnoreCase));

                    if (hasUsage)
                    {
                        calculatedStatus = string.IsNullOrWhiteSpace(profile) ? VoucherStatus.Expired : VoucherStatus.Used;
                    }
                    else
                    {
                        calculatedStatus = VoucherStatus.Unused;
                    }
                }

                var dto = new VoucherDto
                {
                    Id = localFound ? localVoucher!.Id : Guid.NewGuid(),
                    Username = username,
                    Password = string.IsNullOrWhiteSpace(password) && localFound ? localVoucher!.Password : password,
                    Profile = string.IsNullOrWhiteSpace(profile) && localFound ? localVoucher!.ProfileName : profile,
                    Price = localFound ? localVoucher!.Price : 0,
                    Status = calculatedStatus,
                    IsDisabled = string.Equals(GetWord(sentence, "disabled"), "true", StringComparison.OrdinalIgnoreCase),
                    IsDeleted = localFound ? localVoucher!.IsDeleted : false,
                    SyncStatus = SyncStatus.Synced,
                    CreatedAt = localFound ? localVoucher!.CreatedAt : DateTime.UtcNow,
                    BatchId = localFound ? localVoucher!.BatchId : Guid.Empty,
                    CredentialMode = localFound ? localVoucher!.CredentialMode : CredentialMode.UsernameAndPassword,
                    DataOrigin = localFound ? VoucherDataOrigin.RouterMerged : VoucherDataOrigin.RouterOnly,
                    QuotaUsedBytes = used,
                    QuotaLimitBytes = limit,
                    IsQuotaDepleted = depleted,
                    DownloadUsedBytes = downloadUsed,
                    UploadUsedBytes = uploadUsed,
                    RouterTimeHint = BuildRouterTimeHint(sentence, isHotspot),
                    AgentName = localFound ? (localVoucher!.Agent != null ? localVoucher.Agent.Name : "-") : "-"
                };
                result.Add(dto);
            }

            var seenUsernames = new HashSet<string>(result.Select(x => x.Username), StringComparer.OrdinalIgnoreCase);
            var toSoftDeleteInDb = new List<Voucher>();

            foreach (var local in localLookup.Values)
            {
                if (seenUsernames.Contains(local.Username))
                    continue;

                if (!local.IsDeleted)
                {
                    local.IsDeleted = true;
                    toSoftDeleteInDb.Add(local);
                }

                result.Add(new VoucherDto
                {
                    Id = local.Id,
                    Username = local.Username,
                    Password = local.Password,
                    Profile = local.ProfileName,
                    Price = local.Price,
                    Status = local.Status,
                    IsDisabled = false,
                    IsDeleted = true,
                    SyncStatus = local.SyncStatus,
                    CreatedAt = local.CreatedAt,
                    BatchId = local.BatchId,
                    CredentialMode = local.CredentialMode,
                    DataOrigin = VoucherDataOrigin.Local,
                    AgentName = local.Agent != null ? local.Agent.Name : "-",
                    DownloadUsedBytes = local.DownloadUsedBytes,
                    UploadUsedBytes = local.UploadUsedBytes
                });
            }

            if (toSoftDeleteInDb.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var db = await _dbFactory.CreateDbContextAsync(CancellationToken.None);
                        foreach (var v in toSoftDeleteInDb)
                        {
                            db.Entry(v).State = EntityState.Modified;
                            db.Entry(v).Property(x => x.IsDeleted).IsModified = true;
                        }
                        await db.SaveChangesAsync();
                    }
                    catch
                    {
                        // Ignore
                    }
                });
            }

            return (IReadOnlyList<VoucherDto>)result
                .OrderByDescending(v => v.CreatedAt)
                .ThenBy(v => v.Username)
                .ToList();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetHotspotActiveSessionsForUserAsync(string username, CancellationToken cancellationToken = default)
    {
        var host = _settingsService.Get("MikroTik.Host", "");
        var user = _settingsService.Get("MikroTik.Username", "admin");
        var pass = _settingsService.Get("MikroTik.Password", "");

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
            return Array.Empty<string>();

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
            connection.SendTimeout = 8000;
            connection.ReceiveTimeout = 8000;
            connection.Open(host, user, pass);

            var list = new List<string>();
            try
            {
                var actives = connection.CreateCommandAndParameters("/ip/hotspot/active/print").ExecuteList();
                foreach (var s in actives)
                {
                    var u = GetWord(s, "user");
                    if (!string.Equals(u, username, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var ip = GetWord(s, "address");
                    var up = GetWord(s, "uptime");
                    var mac = GetWord(s, "mac-address");
                    list.Add($"IP: {ip} | MAC: {mac} | ظ…ط¯ط©: {up}");
                }
            }
            catch
            {
                return (IReadOnlyList<string>)new List<string> { "ظ„ط§ طھطھظˆظپط± ط¬ظ„ط³ط§طھ Hotspot ط£ظˆ ط§ظ„طµظ„ط§ط­ظٹط© ط؛ظٹط± ظƒط§ظپظٹط©." };
            }

            return list.Count == 0
                ? (IReadOnlyList<string>)new List<string> { "ظ„ط§ طھظˆط¬ط¯ ط¬ظ„ط³ط© ظ†ط´ط·ط© ظ„ظ‡ط°ط§ ط§ظ„ظ…ط³طھط®ط¯ظ… ط­ط§ظ„ظٹط§ظ‹." }
                : list;
        }, cancellationToken);
    }

    private static void ApplyQuotaFromSentence(ITikSentence sentence, bool isHotspot, out long? used, out long? limit, out bool depleted)
    {
        used = null;
        limit = null;
        depleted = false;

        if (isHotspot)
        {
            var bi = TryLong(GetWord(sentence, "bytes-in"));
            var bo = TryLong(GetWord(sentence, "bytes-out"));
            used = bi + bo;
            limit = TryLong(GetWord(sentence, "limit-bytes-total"));
            if (limit is > 0 && used >= limit)
                depleted = true;
            return;
        }

        var rem = TryLong(GetWord(sentence, "remaining-bytes"));
        var tot = TryLong(GetWord(sentence, "bytes-total"));
        if (tot is > 0 && rem.HasValue)
        {
            limit = tot;
            used = Math.Max(0, tot.Value - rem.Value);
            if (rem == 0)
                depleted = true;
        }
    }

    private static bool InferExpiredFromSentence(ITikSentence sentence, bool isHotspot)
    {
        if (string.Equals(GetWord(sentence, "expired"), "true", StringComparison.OrdinalIgnoreCase))
            return true;

        if (isHotspot)
        {
            // ط§ظ†طھظ‡ط§ط، ظ…ط¯ط© ط§ظ„ط§طھطµط§ظ„: ط­ط¯ ط§ظ„طھط´ط؛ظٹظ„ ظٹط·ط§ط¨ظ‚ ظˆظ‚طھ ط§ظ„طھط´ط؛ظٹظ„ (ط´ط§ط¦ط¹ ظپظٹ Hotspot)
            var limU = GetWord(sentence, "limit-uptime");
            var up = GetWord(sentence, "uptime");
            if (!string.IsNullOrEmpty(limU) && limU != "0s" &&
                !string.IsNullOrEmpty(up) &&
                string.Equals(limU, up, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        else
        {
            // User Manager: ط²ظ…ظ† ظ…طھط¨ظ‚ظٹ طµظپط±
            var rem = GetWord(sentence, "remaining-time");
            if (rem == "0s" || string.Equals(rem, "0", StringComparison.OrdinalIgnoreCase))
                return true;
            var remSecs = GetWord(sentence, "remaining-secs");
            if (remSecs == "0")
                return true;
        }

        return false;
    }

    private static string? BuildRouterTimeHint(ITikSentence sentence, bool isHotspot)
    {
        if (isHotspot)
        {
            var u = GetWord(sentence, "uptime");
            var lim = GetWord(sentence, "limit-uptime");
            if (!string.IsNullOrEmpty(u) || !string.IsNullOrEmpty(lim))
                return $"طھط´ط؛ظٹظ„: {u} / ط­ط¯: {lim}".Trim();
        }
        else
        {
            var v = GetWord(sentence, "validity");
            if (!string.IsNullOrEmpty(v))
                return $"طµظ„ط§ط­ظٹط©: {v}";
        }
        return null;
    }

    private static long? TryLong(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return long.TryParse(s, out var v) ? v : null;
    }

    private static string GetWord(ITikSentence sentence, string key)
    {
        return sentence.Words.TryGetValue(key, out var value) ? value : string.Empty;
    }

    // =====================================================================================================
    //  V2 PAGINATED & SMART QUERY IMPLEMENTATION
    // =====================================================================================================

    public async Task<PagedResult<VoucherDto>> GetVouchersPagedAsync(VoucherQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        
        // 1. Start query chain
        var query = db.Vouchers
            .IgnoreQueryFilters() // Ignore global filter to apply custom logical isolation dynamically
            .Include(v => v.Agent)
            .AsNoTracking();

        // Apply dynamic query filters
        query = ApplyQueryParameters(query, parameters);

        // 2. Count total matches matching the query
        int totalCount = await query.CountAsync(cancellationToken);

        // 3. Apply sorting and pagination
        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(v => new VoucherDto
            {
                Id             = v.Id,
                Username       = v.Username,
                Password       = v.Password,
                Profile        = v.ProfileName,
                Price          = v.Price,
                Status         = v.Status,
                IsDisabled     = false, // المحلي ليس معطلاً بطبعه
                IsDeleted      = v.IsDeleted,
                SyncStatus     = v.SyncStatus,
                CreatedAt      = v.CreatedAt,
                BatchId        = v.BatchId,
                CredentialMode = v.CredentialMode,
                DataOrigin     = VoucherDataOrigin.Local,
                AgentName      = v.Agent != null ? v.Agent.Name : "-",
                QuotaUsedBytes = null,
                QuotaLimitBytes = null,
                IsQuotaDepleted = false,
                DownloadUsedBytes = v.DownloadUsedBytes,
                UploadUsedBytes = v.UploadUsedBytes,
                VoucherSource = v.VoucherSource,
                ImportDate = v.ImportDate,
                CreatedBy = v.CreatedBy,
                Comment = v.Comment
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<VoucherDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<PagedResult<VoucherDto>> GetVouchersKeysetAsync(
        VoucherQueryParameters parameters,
        DateTime? afterCreatedAt,
        Guid? afterId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        
        var query = db.Vouchers
            .IgnoreQueryFilters()
            .Include(v => v.Agent)
            .AsNoTracking();

        query = ApplyQueryParameters(query, parameters);

        // Keyset Cursor Pagination
        if (afterCreatedAt.HasValue && afterId.HasValue)
        {
            query = query.Where(v => 
                v.CreatedAt < afterCreatedAt.Value ||
                (v.CreatedAt == afterCreatedAt.Value && v.Id.CompareTo(afterId.Value) < 0));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Take(pageSize)
            .Select(v => new VoucherDto
            {
                Id             = v.Id,
                Username       = v.Username,
                Password       = v.Password,
                Profile        = v.ProfileName,
                Price          = v.Price,
                Status         = v.Status,
                IsDisabled     = false,
                IsDeleted      = v.IsDeleted,
                SyncStatus     = v.SyncStatus,
                CreatedAt      = v.CreatedAt,
                BatchId        = v.BatchId,
                CredentialMode = v.CredentialMode,
                DataOrigin     = VoucherDataOrigin.Local,
                AgentName      = v.Agent != null ? v.Agent.Name : "-",
                QuotaUsedBytes = null,
                QuotaLimitBytes = null,
                IsQuotaDepleted = false,
                DownloadUsedBytes = v.DownloadUsedBytes,
                UploadUsedBytes = v.UploadUsedBytes,
                VoucherSource = v.VoucherSource,
                ImportDate = v.ImportDate,
                CreatedBy = v.CreatedBy,
                Comment = v.Comment
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<VoucherDto>(items, totalCount, 1, pageSize);
    }

    public async Task<int> GetVoucherCountAsync(VoucherQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        
        var query = db.Vouchers
            .IgnoreQueryFilters()
            .AsNoTracking();

        query = ApplyQueryParameters(query, parameters);

        return await query.CountAsync(cancellationToken);
    }

    private IQueryable<Voucher> ApplyQueryParameters(IQueryable<Voucher> query, VoucherQueryParameters parameters)
    {
        // A. Logical Router Isolation & Soft Delete Filter
        if (parameters.SelectedNodeCategory?.ToLowerInvariant() == "recyclebin")
        {
            query = query.Where(v => v.RouterId == parameters.RouterId && v.IsDeleted);
        }
        else
        {
            query = query.Where(v => v.RouterId == parameters.RouterId && !v.IsDeleted);
        }

        // B. Apply Sidebar Node constraints
        if (!string.IsNullOrWhiteSpace(parameters.SelectedNodeCategory))
        {
            var category = parameters.SelectedNodeCategory.ToLowerInvariant();
            var val = parameters.SelectedNodeValue;

            if (category == "unassigned")
            {
                query = query.Where(v => v.BatchId == Guid.Empty);
            }
            else if (category == "batches" && Guid.TryParse(val, out var batchId))
            {
                query = query.Where(v => v.BatchId == batchId);
            }
            else if (category == "agents" && Guid.TryParse(val, out var agentId))
            {
                query = query.Where(v => v.AgentId == agentId);
            }
            else if (category == "profiles" && !string.IsNullOrWhiteSpace(val))
            {
                query = query.Where(v => v.ProfileName == val);
            }
            else if (category == "imported")
            {
                query = query.Where(v => v.VoucherSource == VoucherSource.ImportedFromRouter);
            }
        }

        // C. Apply General Filters (If not overridden by Sidebar Node)
        if (parameters.FilterStatus != "All")
        {
            var statusMatch = parameters.FilterStatus switch
            {
                "Used"     => VoucherStatus.Used,
                "Unused"   => VoucherStatus.Unused,
                "Expired"  => VoucherStatus.Expired,
                _          => (VoucherStatus?)null
            };
            if (statusMatch.HasValue)
            {
                query = query.Where(v => v.Status == statusMatch.Value);
            }
            else if (parameters.FilterStatus == "Disabled")
            {
                // المحلي ليس له حالة IsDisabled في قاعدة البيانات
            }
        }

        if (parameters.FilterSync != "All")
        {
            var syncMatch = parameters.FilterSync switch
            {
                "Synced"  => SyncStatus.Synced,
                "Pending" => SyncStatus.Pending,
                "Failed"  => SyncStatus.Failed,
                _         => (SyncStatus?)null
            };
            if (syncMatch.HasValue)
            {
                query = query.Where(v => v.SyncStatus == syncMatch.Value);
            }
        }

        if (parameters.FilterProfile != "كل الباقات" && !string.IsNullOrWhiteSpace(parameters.FilterProfile))
        {
            query = query.Where(v => v.ProfileName == parameters.FilterProfile);
        }

        // D. Smart Query Parser (Tag-based search logic on the database level)
        if (!string.IsNullOrWhiteSpace(parameters.SearchText))
        {
            var rawText = parameters.SearchText.Trim();
            
            // Regex to parse tags: key:value
            var parts = System.Text.RegularExpressions.Regex.Matches(rawText, @"(\w+):""([^""]+)""|(\w+):(\S+)|(\S+)");
            
            bool hasStructuredFilters = false;

            foreach (System.Text.RegularExpressions.Match match in parts)
            {
                if (match.Groups[1].Success) // key:"value"
                {
                    var key = match.Groups[1].Value.ToLowerInvariant();
                    var val = match.Groups[2].Value;
                    query = ApplySearchTag(query, key, val, parameters.IsExactSearch);
                    hasStructuredFilters = true;
                }
                else if (match.Groups[3].Success) // key:value
                {
                    var key = match.Groups[3].Value.ToLowerInvariant();
                    var val = match.Groups[4].Value;
                    query = ApplySearchTag(query, key, val, parameters.IsExactSearch);
                    hasStructuredFilters = true;
                }
                else if (match.Groups[5].Success && !hasStructuredFilters) // flat search term
                {
                    var term = match.Groups[5].Value;
                    if (parameters.IsExactSearch)
                    {
                        query = query.Where(v => v.Username == term || 
                                                 (v.ProfileName != null && v.ProfileName == term));
                    }
                    else
                    {
                        query = query.Where(v => v.Username.Contains(term) || 
                                                 (v.ProfileName != null && v.ProfileName.Contains(term)));
                    }
                }
            }
        }

        return query;
    }

    private IQueryable<Voucher> ApplySearchTag(IQueryable<Voucher> query, string key, string value, bool isExact)
    {
        switch (key)
        {
            case "user":
            case "username":
                return isExact ? query.Where(v => v.Username == value) : query.Where(v => v.Username.Contains(value));
            case "profile":
            case "package":
                return isExact ? query.Where(v => v.ProfileName == value) : query.Where(v => v.ProfileName != null && v.ProfileName.Contains(value));
            case "agent":
            case "distributor":
                return isExact ? query.Where(v => v.Agent != null && v.Agent.Name == value) : query.Where(v => v.Agent != null && v.Agent.Name.Contains(value));
            case "status":
                var sVal = value.ToLowerInvariant();
                if (sVal == "used" || sVal == "مستخدم")
                    return query.Where(v => v.Status == VoucherStatus.Used);
                if (sVal == "unused" || sVal == "غير مستخدم")
                    return query.Where(v => v.Status == VoucherStatus.Unused);
                if (sVal == "expired" || sVal == "منتهي")
                    return query.Where(v => v.Status == VoucherStatus.Expired);
                break;
            case "batch":
                if (Guid.TryParse(value, out var batchId))
                {
                    return query.Where(v => v.BatchId == batchId);
                }
                break;
        }
        return query;
    }
}

