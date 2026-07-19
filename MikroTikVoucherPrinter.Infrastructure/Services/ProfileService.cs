using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Infrastructure.Data;
using MikroTikVoucherPrinter.Domain.Enums;
namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// خدمة الباقات — تجلب وتحدث الباقات مباشرة من المايكروتك عبر IMikroTikCommandExecutor.
/// تعتمد على IRouterCapabilityService لمعرفة نوع النظام.
/// تحتفظ بنسخة طوارئ (Read Only Emergency Snapshot) في قاعدة البيانات.
/// </summary>
public class ProfileService : IProfileService
{
    private static readonly string TraceLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lux_trace.txt");
    private static readonly string ErrorLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lux_error.txt");
    private readonly IMikroTikCommandExecutor _commandExecutor;
    private readonly IRouterCapabilityService _capabilityService;
    private readonly IActiveRouterContext _routerContext;
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        IMikroTikCommandExecutor commandExecutor,
        IRouterCapabilityService capabilityService,
        IActiveRouterContext routerContext,
        IDbContextFactory<LuxCardDbContext> dbFactory,
        ILogger<ProfileService> logger)
    {
        _commandExecutor = commandExecutor;
        _capabilityService = capabilityService;
        _routerContext = routerContext;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    private void EnsureConnected()
    {
        if (!_routerContext.IsConnected || _routerContext.CurrentRouter == null)
            throw new Exception("الراوتر غير متصل حالياً. تأكد من اتصال المايكروتك أولاً.");
    }

    // ═══════════════════════════════════════════════════════════
    //  Helper
    // ═══════════════════════════════════════════════════════════
    private async Task<string> ResolveSystemTypeAsync(PackageSourceType sourceType, CancellationToken ct)
    {
        if (sourceType == PackageSourceType.Hotspot)
            return "Hotspot";
            
        var cap = await _capabilityService.GetProfileSystemTypeAsync(ct);
        if (cap == "Hotspot")
            throw new Exception("حزمة User Manager غير مثبتة أو غير مفعلة على هذا الراوتر.");
            
        return cap; // "UMv7" or "UMv6"
    }

    // ═══════════════════════════════════════════════════════════
    //  جلب الباقات من المايكروتك
    // ═══════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<Profile>> GetAllProfilesAsync(PackageSourceType sourceType, CancellationToken cancellationToken = default)
    {
        try
        {
            var liveProfiles = await FetchFromMikroTikAsync(sourceType, cancellationToken);

            if (liveProfiles.Count > 0)
            {
                await UpdateEmergencySnapshotAsync(sourceType, liveProfiles, cancellationToken);
                _logger.LogInformation("✅ [ProfileService] تم جلب {Count} باقة مباشرة من المايكروتك.", liveProfiles.Count);
                
                // --- TRACE: Service Return (Live) ---
                var t = new System.Text.StringBuilder();
                t.AppendLine($"\n=== SERVICE RETURN (MikroTik Live) ===");
                t.AppendLine($"SelectedSource: {sourceType}");
                foreach(var p in liveProfiles) t.AppendLine($"- {p.Name} | {p.SystemType}");
                System.IO.File.AppendAllText(TraceLogPath, t.ToString());
                // ------------------------------------
                
                return liveProfiles;
            }
        }
        catch (Exception ex)
        {
            try { System.IO.File.AppendAllText(ErrorLogPath, $"Fetch Error: {ex}\n"); } catch {}
            _logger.LogWarning("⚠️ [ProfileService] تعذر الاتصال بالمايكروتك: {Err} — سيتم تحميل نسخة الطوارئ.", ex.Message);
        }

        // Fallback: Read Only Emergency Snapshot
        return await LoadEmergencySnapshotAsync(sourceType, cancellationToken);
    }

    private async Task<List<Profile>> FetchFromMikroTikAsync(PackageSourceType sourceType, CancellationToken cancellationToken)
    {
        EnsureConnected();
        
        string systemType = await ResolveSystemTypeAsync(sourceType, cancellationToken);
        string cmd = systemType == "UMv7" ? "/user-manager/profile/print" : 
                     systemType == "UMv6" ? "/tool/user-manager/profile/print" : 
                     "/ip/hotspot/user/profile/print";

        var response = await _commandExecutor.ExecuteAsync(new MikroTikCommand { Command = cmd }, cancellationToken);
        var dicts = response.RawData;

        IEnumerable<IReadOnlyDictionary<string, string>> limits = null;
        if (systemType == "UMv6")
        {
            try
            {
                var limitsResponse = await _commandExecutor.ExecuteAsync(new MikroTikCommand { Command = "/tool/user-manager/profile/limitation/print" }, cancellationToken);
                limits = limitsResponse.RawData;
            }
            catch { /* Ignore */ }
        }

        var list = new List<Profile>();

        foreach (var d in dicts)
        {
            if (!d.TryGetValue("name", out string name) || string.IsNullOrEmpty(name)) continue;
            if (name == "default") continue;

            string priceStr = "0", validity = "", transfer = "", uptime = "", rateLimit = "", sharedUsers = "1";

            if (systemType == "Hotspot")
            {
                d.TryGetValue("rate-limit", out rateLimit);
                d.TryGetValue("session-timeout", out uptime);
                d.TryGetValue("shared-users", out sharedUsers);
            }
            else
            {
                d.TryGetValue("price", out priceStr);
                d.TryGetValue("validity", out validity);
                d.TryGetValue("rate-limit", out rateLimit);
                d.TryGetValue("shared-users", out sharedUsers);

                if (limits != null)
                {
                    var myLimit = limits.FirstOrDefault(l => l.TryGetValue("name", out var ln) && ln == name);
                    if (myLimit != null)
                    {
                        if (myLimit.TryGetValue("transfer-limit", out var transRaw)) transfer = FormatBytes(transRaw);
                        if (myLimit.TryGetValue("uptime-limit", out var upRaw)) uptime = upRaw;
                    }
                }
            }

            decimal.TryParse(priceStr ?? "0", out decimal price);
            d.TryGetValue(".id", out var internalId);

            list.Add(new Profile
            {
                Id = Guid.NewGuid(),
                Name = name,
                MikroTikProfileId = internalId,
                Price = price,
                Duration = validity ?? "",
                Transfer = transfer,
                Uptime = uptime ?? "",
                RateLimit = rateLimit ?? "",
                SharedUsers = sharedUsers ?? "1",
                RouterHost = _routerContext.CurrentRouter!.Host,
                LastSyncedAt = DateTime.UtcNow,
                IsFromCache = false,
                SystemType = systemType == "Hotspot" ? "Hotspot" : "UserManager"
            });
        }

        return list.OrderBy(p => p.Name).ToList();
    }

    private async Task<IReadOnlyList<Profile>> LoadEmergencySnapshotAsync(PackageSourceType sourceType, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var cached = await db.Profiles
                .Where(p => p.RouterHost == _routerContext.CurrentRouter!.Host)
                .Where(p => (sourceType == PackageSourceType.Hotspot && p.SystemType == "Hotspot") || 
                            (sourceType == PackageSourceType.UserManager && p.SystemType == "UserManager"))
                .OrderBy(p => p.Name)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var p in cached) p.IsFromCache = true;

            // --- TRACE: Database EF Return ---
            var tdb = new System.Text.StringBuilder();
            tdb.AppendLine($"\n=== DATABASE TRACE (EF Return) ===");
            foreach(var r in cached) tdb.AppendLine($"- Id={r.Id}, Name={r.Name}, SystemType={r.SystemType}");
            System.IO.File.AppendAllText(TraceLogPath, tdb.ToString());
            
            // --- TRACE: Service Return (Snapshot) ---
            var ts = new System.Text.StringBuilder();
            ts.AppendLine($"\n=== SERVICE RETURN (Emergency Snapshot) ===");
            ts.AppendLine($"SelectedSource: {sourceType}");
            foreach(var p in cached) ts.AppendLine($"- {p.Name} | {p.SystemType}");
            System.IO.File.AppendAllText(TraceLogPath, ts.ToString());
            // ----------------------------------------

            _logger.LogInformation("🗃️ [ProfileService] تم تحميل {Count} باقة من نسخة الطوارئ.", cached.Count);
            return cached;
        }
        catch (Exception ex)
        {
            _logger.LogError("❌ [ProfileService] فشل تحميل نسخة الطوارئ: {Err}", ex.Message);
            return new List<Profile>();
        }
    }

    private async Task UpdateEmergencySnapshotAsync(PackageSourceType sourceType, List<Profile> liveProfiles, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var host = _routerContext.CurrentRouter!.Host;
            var routerId = _routerContext.CurrentRouter!.Id;

            var existing = await db.Profiles
                .Where(p => p.RouterId == routerId)
                .Where(p => (sourceType == PackageSourceType.Hotspot && p.SystemType == "Hotspot") || 
                            (sourceType == PackageSourceType.UserManager && p.SystemType == "UserManager"))
                .ToListAsync(cancellationToken);

            var liveMap = liveProfiles.Where(p => !string.IsNullOrEmpty(p.SystemType)).ToList();

            foreach (var live in liveMap)
            {
                var match = existing.FirstOrDefault(p => !string.IsNullOrEmpty(p.MikroTikProfileId) && 
                                                         p.MikroTikProfileId.Equals(live.MikroTikProfileId, StringComparison.OrdinalIgnoreCase));
                
                if (match == null)
                {
                    match = existing.FirstOrDefault(p => p.Name.Equals(live.Name, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.MikroTikProfileId = live.MikroTikProfileId;
                    }
                }

                if (match != null)
                {
                    match.Name = live.Name;
                    match.Duration = live.Duration;
                    match.Transfer = live.Transfer;
                    match.Uptime = live.Uptime;
                    match.RateLimit = live.RateLimit;
                    match.SharedUsers = live.SharedUsers;
                    match.LastSyncedAt = DateTime.UtcNow;
                    match.IsFromCache = false;

                    db.Entry(match).State = EntityState.Modified;
                    existing.Remove(match);
                }
                else
                {
                    live.RouterId = routerId;
                    await db.Profiles.AddAsync(live, cancellationToken);
                }
            }

            if (existing.Any())
            {
                db.Profiles.RemoveRange(existing);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ [ProfileService] فشل تحديث الكاش: {Err}", ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  إنشاء باقة في المايكروتك
    // ═══════════════════════════════════════════════════════════
    public async Task<Profile> CreateProfileAsync(PackageSourceType sourceType, string name, string validity, string transfer, string uptime,
        string rateLimit, string sharedUsers, decimal price, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        string systemType = await ResolveSystemTypeAsync(sourceType, cancellationToken);

        try
        {
            if (systemType == "UMv7")
            {
                var cmd = new MikroTikCommand { Command = "/user-manager/profile/add" };
                cmd.Parameters.Add("name", name);
                cmd.Parameters.Add("starts-when", "first-auth");
                cmd.Parameters.Add("validity", validity);
                cmd.Parameters.Add("price", price.ToString("F2"));
                if (!string.IsNullOrEmpty(rateLimit))
                {
                    cmd.Parameters.Add("rate-limit-rx", rateLimit);
                    cmd.Parameters.Add("rate-limit-tx", rateLimit);
                }
                cmd.Parameters.Add("shared-users", sharedUsers);
                await _commandExecutor.ExecuteAsync(cmd, cancellationToken);
            }
            else if (systemType == "UMv6")
            {
                var cmd = new MikroTikCommand { Command = "/tool/user-manager/profile/add" };
                cmd.Parameters.Add("name", name);
                cmd.Parameters.Add("validity", validity);
                cmd.Parameters.Add("price", price.ToString("F2"));
                cmd.Parameters.Add("shared-users", sharedUsers);
                await _commandExecutor.ExecuteAsync(cmd, cancellationToken);

                if (!string.IsNullOrEmpty(transfer) || !string.IsNullOrEmpty(uptime) || !string.IsNullOrEmpty(rateLimit))
                {
                    var limCmd = new MikroTikCommand { Command = "/tool/user-manager/profile/limitation/add" };
                    limCmd.Parameters.Add("name", name);
                    if (!string.IsNullOrEmpty(transfer)) limCmd.Parameters.Add("transfer-limit", transfer);
                    if (!string.IsNullOrEmpty(uptime)) limCmd.Parameters.Add("uptime-limit", uptime);
                    if (!string.IsNullOrEmpty(rateLimit))
                    {
                        limCmd.Parameters.Add("rate-limit-rx", rateLimit);
                        limCmd.Parameters.Add("rate-limit-tx", rateLimit);
                    }
                    await _commandExecutor.ExecuteAsync(limCmd, cancellationToken);
                }
            }
            else // Hotspot
            {
                var cmd = new MikroTikCommand { Command = "/ip/hotspot/user/profile/add" };
                cmd.Parameters.Add("name", name);
                if (!string.IsNullOrEmpty(rateLimit)) cmd.Parameters.Add("rate-limit", rateLimit);
                if (!string.IsNullOrEmpty(uptime)) cmd.Parameters.Add("session-timeout", uptime);
                cmd.Parameters.Add("shared-users", sharedUsers);
                await _commandExecutor.ExecuteAsync(cmd, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("تعذر إنشاء الباقة في المايكروتك. تأكد من صحة الصلاحيات.", ex);
        }

        return new Profile
        {
            Id = Guid.NewGuid(),
            Name = name, Duration = validity, Transfer = transfer,
            Uptime = uptime, RateLimit = rateLimit, SharedUsers = sharedUsers,
            Price = price, RouterHost = _routerContext.CurrentRouter!.Host, LastSyncedAt = DateTime.UtcNow,
            SystemType = systemType == "Hotspot" ? "Hotspot" : "UserManager"
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  تحديث باقة في المايكروتك
    // ═══════════════════════════════════════════════════════════
    public Task UpdateProfileAsync(PackageSourceType sourceType, string name, string validity, string transfer, string uptime,
        string sharedUsers, decimal price, CancellationToken cancellationToken = default)
        => UpdateProfileAsync(sourceType, new Profile
        {
            Name = name, Duration = validity, Transfer = transfer,
            Uptime = uptime, SharedUsers = sharedUsers, Price = price
        }, cancellationToken);

    public async Task UpdateProfileAsync(PackageSourceType sourceType, Profile profile, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        string systemType = await ResolveSystemTypeAsync(sourceType, cancellationToken);

        try
        {
            if (systemType == "UMv7")
            {
                var printCmd = new MikroTikCommand { Command = "/user-manager/profile/print" };
                printCmd.Parameters.Add("name", profile.Name);
                var pList = await _commandExecutor.ExecuteAsync(printCmd, cancellationToken);
                
                var first = pList.RawData.FirstOrDefault();
                if (first != null && first.TryGetValue(".id", out string id))
                {
                    var setCmd = new MikroTikCommand { Command = "/user-manager/profile/set" };
                    setCmd.Parameters.Add(".id", id);
                    setCmd.Parameters.Add("validity", profile.Duration ?? "");
                    setCmd.Parameters.Add("price", profile.Price.ToString("F2"));
                    setCmd.Parameters.Add("shared-users", profile.SharedUsers ?? "1");
                    await _commandExecutor.ExecuteAsync(setCmd, cancellationToken);
                }
                else throw new Exception("الباقة غير موجودة في المايكروتك.");
            }
            else if (systemType == "UMv6")
            {
                var printCmd = new MikroTikCommand { Command = "/tool/user-manager/profile/print" };
                printCmd.Parameters.Add("name", profile.Name);
                var pList = await _commandExecutor.ExecuteAsync(printCmd, cancellationToken);
                
                var first = pList.RawData.FirstOrDefault();
                if (first != null && first.TryGetValue(".id", out string id))
                {
                    var setCmd = new MikroTikCommand { Command = "/tool/user-manager/profile/set" };
                    setCmd.Parameters.Add(".id", id);
                    setCmd.Parameters.Add("validity", profile.Duration ?? "");
                    setCmd.Parameters.Add("price", profile.Price.ToString("F2"));
                    setCmd.Parameters.Add("shared-users", profile.SharedUsers ?? "1");
                    await _commandExecutor.ExecuteAsync(setCmd, cancellationToken);
                    
                    if (!string.IsNullOrEmpty(profile.Transfer) || !string.IsNullOrEmpty(profile.Uptime))
                    {
                        var limPrintCmd = new MikroTikCommand { Command = "/tool/user-manager/profile/limitation/print" };
                        limPrintCmd.Parameters.Add("name", profile.Name);
                        var limits = await _commandExecutor.ExecuteAsync(limPrintCmd, cancellationToken);
                        var limFirst = limits.RawData.FirstOrDefault();
                        if (limFirst != null && limFirst.TryGetValue(".id", out string limitId))
                        {
                            var limSetCmd = new MikroTikCommand { Command = "/tool/user-manager/profile/limitation/set" };
                            limSetCmd.Parameters.Add(".id", limitId);
                            if (!string.IsNullOrEmpty(profile.Transfer)) limSetCmd.Parameters.Add("transfer-limit", profile.Transfer);
                            if (!string.IsNullOrEmpty(profile.Uptime)) limSetCmd.Parameters.Add("uptime-limit", profile.Uptime);
                            await _commandExecutor.ExecuteAsync(limSetCmd, cancellationToken);
                        }
                    }
                }
                else throw new Exception("الباقة غير موجودة في المايكروتك.");
            }
            else // Hotspot
            {
                var printCmd = new MikroTikCommand { Command = "/ip/hotspot/user/profile/print" };
                printCmd.Parameters.Add("name", profile.Name);
                var pList = await _commandExecutor.ExecuteAsync(printCmd, cancellationToken);
                
                var first = pList.RawData.FirstOrDefault();
                if (first != null && first.TryGetValue(".id", out string id))
                {
                    var setCmd = new MikroTikCommand { Command = "/ip/hotspot/user/profile/set" };
                    setCmd.Parameters.Add(".id", id);
                    if (!string.IsNullOrEmpty(profile.RateLimit)) setCmd.Parameters.Add("rate-limit", profile.RateLimit);
                    if (!string.IsNullOrEmpty(profile.Uptime)) setCmd.Parameters.Add("session-timeout", profile.Uptime);
                    setCmd.Parameters.Add("shared-users", profile.SharedUsers ?? "1");
                    await _commandExecutor.ExecuteAsync(setCmd, cancellationToken);
                }
                else throw new Exception("الباقة غير موجودة في المايكروتك.");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"فشل تحديث الباقة '{profile.Name}': {ex.Message}", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  حذف باقة من المايكروتك
    // ═══════════════════════════════════════════════════════════
    public Task DeleteProfileAsync(PackageSourceType sourceType, Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("يرجى استخدام الحذف بواسطة الاسم (DeleteProfileByNameAsync)");
    }

    public async Task DeleteProfileByNameAsync(PackageSourceType sourceType, string name, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        string systemType = await ResolveSystemTypeAsync(sourceType, cancellationToken);

        try
        {
            if (systemType == "UMv7")
            {
                var printCmd = new MikroTikCommand { Command = "/user-manager/profile/print" };
                printCmd.Parameters.Add("name", name);
                var profiles = await _commandExecutor.ExecuteAsync(printCmd, cancellationToken);
                
                var first = profiles.RawData.FirstOrDefault();
                if (first != null && first.TryGetValue(".id", out string id))
                {
                    var rmCmd = new MikroTikCommand { Command = "/user-manager/profile/remove" };
                    rmCmd.Parameters.Add(".id", id);
                    await _commandExecutor.ExecuteAsync(rmCmd, cancellationToken);
                }
            }
            else if (systemType == "UMv6")
            {
                var printCmd = new MikroTikCommand { Command = "/tool/user-manager/profile/print" };
                printCmd.Parameters.Add("name", name);
                var profiles = await _commandExecutor.ExecuteAsync(printCmd, cancellationToken);
                
                var first = profiles.RawData.FirstOrDefault();
                if (first != null && first.TryGetValue(".id", out string id))
                {
                    var rmCmd = new MikroTikCommand { Command = "/tool/user-manager/profile/remove" };
                    rmCmd.Parameters.Add(".id", id);
                    await _commandExecutor.ExecuteAsync(rmCmd, cancellationToken);
                }
            }
            else // Hotspot
            {
                var printCmd = new MikroTikCommand { Command = "/ip/hotspot/user/profile/print" };
                printCmd.Parameters.Add("name", name);
                var profiles = await _commandExecutor.ExecuteAsync(printCmd, cancellationToken);
                
                var first = profiles.RawData.FirstOrDefault();
                if (first != null && first.TryGetValue(".id", out string id))
                {
                    var rmCmd = new MikroTikCommand { Command = "/ip/hotspot/user/profile/remove" };
                    rmCmd.Parameters.Add(".id", id);
                    await _commandExecutor.ExecuteAsync(rmCmd, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"فشل حذف الباقة '{name}': {ex.Message}", ex);
        }
    }

    private static string FormatBytes(string bytesStr)
    {
        if (long.TryParse(bytesStr, out long b))
        {
            if (b >= 1024L * 1024 * 1024) return $"{b / (1024L * 1024 * 1024)} GB";
            if (b >= 1024 * 1024) return $"{b / (1024 * 1024)} MB";
            return $"{b} B";
        }
        return bytesStr;
    }
}
