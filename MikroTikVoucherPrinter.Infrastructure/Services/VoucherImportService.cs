using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Infrastructure.Data;
using tik4net;
using Lux.Platform.Abstractions.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class VoucherImportService : IVoucherImportService
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly IDbContextFactory<PlatformDbContext> _platformDbFactory;
    private readonly ISecureStorageService _secureStorageService;

    public VoucherImportService(
        IDbContextFactory<LuxCardDbContext> dbFactory,
        IDbContextFactory<PlatformDbContext> platformDbFactory,
        ISecureStorageService secureStorageService)
    {
        _dbFactory = dbFactory;
        _platformDbFactory = platformDbFactory;
        _secureStorageService = secureStorageService;
    }

    public async Task<bool> IsImportRequiredAsync(Guid routerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        
        // 1. تحقق إذا كان يوجد أي كروت محلية غير محذوفة للراوتر الحالي
        var localCount = await db.Vouchers
            .IgnoreQueryFilters()
            .CountAsync(v => v.RouterId == routerId && !v.IsDeleted, cancellationToken);
            
        return localCount == 0;
    }

    public async Task ImportVouchersAsync(Guid routerId, Action<int, int> progressCallback, CancellationToken cancellationToken = default)
    {
        // 1. جلب بيانات الاتصال للراوتر النشط
        await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
        var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
        if (router == null)
            throw new InvalidOperationException("الراوتر المحدد غير موجود بقاعدة البيانات");

        var pass = "";
        if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
        {
            try
            {
                pass = _secureStorageService.Decrypt(router.EncryptedPassword);
            }
            catch
            {
                // تجاهل
            }
        }

        // 2. قراءة باقات الراوتر محلياً لتطبيق استراتيجية استنتاج السعر
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var localProfiles = await db.Profiles
            .Where(p => p.RouterId == routerId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var profilePriceLookup = localProfiles
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(p => p.Name, p => p.Price, StringComparer.OrdinalIgnoreCase);

        // 3. إنشاء الدفعة المخصصة للاستيراد
        var legacyBatchName = $"LEGACY-IMPORT-{DateTime.Now:yyyyMMdd-HHmm}";
        var batchId = Guid.NewGuid();
        var legacyBatch = new Batch
        {
            Id = batchId,
            Name = legacyBatchName,
            ProfileName = "Legacy",
            TotalCards = 0,
            RouterId = routerId
        };
        
        db.Batches.Add(legacyBatch);
        await db.SaveChangesAsync(cancellationToken);

        var logPath = @"C:\Users\MrAziz\.gemini\antigravity\brain\6bb8795a-6087-4e0f-984f-b7e2636f66c8\import_diagnostics.log";
        var log = new Action<string>((msg) =>
        {
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
            }
            catch { }
        });

        log("══════════════════════════════════════════════════════");
        log($"بدء عملية الاستيراد للراوتر ID: {routerId}");
        log($"اسم الدفعة المنشأة: {legacyBatchName}");
        log($"عدد الباقات المحلية المسترجعة لـ lookup الأسعار: {localProfiles.Count}");

        // 4. الاتصال بالراوتر وجلب كود المستخدمين
        List<ITikSentence> rawUsers = new();
        bool isHotspot = false;

        log($"محاولة الاتصال بالراوتر على العنوان: {router.Host}، المستخدم: {router.Username}");
        try
        {
            await Task.Run(() =>
            {
                using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                connection.SendTimeout = 30000;
                connection.ReceiveTimeout = 30000;
                connection.Open(router.Host, router.Username, pass);
                log("تم فتح الاتصال بالراوتر بنجاح.");

                log("محاولة تنفيذ: /tool/user-manager/user/print");
                try
                {
                    rawUsers = connection.CreateCommandAndParameters("/tool/user-manager/user/print").ExecuteList().Cast<ITikSentence>().ToList();
                    log($"نجح تنفيذ /tool/user-manager/user/print. عدد المستخدمين: {rawUsers.Count}");
                }
                catch (Exception ex1)
                {
                    log($"فشل /tool/user-manager/user/print. الخطأ: {ex1.Message}");
                    log("محاولة تنفيذ البديل: /user-manager/user/print");
                    try
                    {
                        rawUsers = connection.CreateCommandAndParameters("/user-manager/user/print").ExecuteList().Cast<ITikSentence>().ToList();
                        log($"نجح تنفيذ /user-manager/user/print. عدد المستخدمين: {rawUsers.Count}");
                    }
                    catch (Exception ex2)
                    {
                        log($"فشل /user-manager/user/print. الخطأ: {ex2.Message}");
                        log("محاولة تنفيذ Hotspot كخيار أخير: /ip/hotspot/user/print");
                        try
                        {
                            rawUsers = connection.CreateCommandAndParameters("/ip/hotspot/user/print").ExecuteList().Cast<ITikSentence>().ToList();
                            isHotspot = true;
                            log($"نجح تنفيذ /ip/hotspot/user/print. عدد المستخدمين: {rawUsers.Count}");
                        }
                        catch (Exception ex3)
                        {
                            log($"فشلت جميع محاولات جلب المستخدمين من الراوتر. الخطأ الأخير: {ex3.Message}");
                            throw;
                        }
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            log($"خطأ حرج أثناء الاتصال أو القراءة من الراوتر: {ex}");
            throw;
        }

        log($"إجمالي عدد السجلات الخام المسترجعة من الراوتر: {rawUsers.Count}");
        if (!rawUsers.Any())
        {
            log("لم يتم العثور على أي مستخدمين على الراوتر. إنهاء عملية الاستيراد.");
            return; // لا يوجد مستخدمين للاستيراد
        }

        int totalCount = rawUsers.Count;
        int processedCount = 0;
        
        // استدعاء التحديث الأولي لواجهة المستخدم بالعدد الإجمالي المستهدف فوراً
        progressCallback?.Invoke(0, totalCount);
        log($"تم استدعاء callback الأولي بـ 0 من أصل {totalCount}");

        // 5. استيراد على دفعات صغيرة لتفادي تجميد الذاكرة وقاعدة البيانات (Chunks of 2000)
        var chunk = new List<Voucher>();
        var seenUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // جلب أسماء المستخدمين المحليين مسبقاً لحماية التكرار
        var existingUsernames = await db.Vouchers
            .IgnoreQueryFilters()
            .Where(v => v.RouterId == routerId)
            .Select(v => v.Username)
            .ToListAsync(cancellationToken);

        foreach (var u in existingUsernames)
        {
            seenUsernames.Add(u);
        }

        // بدء Transaction صريحة لتحقيق أقصى سرعة وحفظ سلامة البيانات في SQLite
        using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var sentence in rawUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var username = GetWord(sentence, isHotspot ? "name" : "username");
            if (string.IsNullOrWhiteSpace(username) || seenUsernames.Contains(username))
            {
                processedCount++;
                continue; // تخطي الفارغ والمكرر لحماية التكرار
            }

            var password = GetWord(sentence, "password") ?? "";
            var profile = GetWord(sentence, isHotspot ? "profile" : "profile") ?? "";
            if (string.IsNullOrWhiteSpace(profile))
            {
                profile = GetWord(sentence, "actual-profile") ?? "";
            }

            var comment = GetWord(sentence, "comment");

            // استنتاج السعر تلقائياً
            decimal? inferredPrice = null;
            if (!string.IsNullOrWhiteSpace(profile) && profilePriceLookup.TryGetValue(profile, out var price))
            {
                inferredPrice = price;
            }

            // تحديد الحالة
            var calculatedStatus = VoucherStatus.Unused;
            var expired = InferExpiredFromSentence(sentence, isHotspot);
            
            long? downloadUsed = null;
            long? uploadUsed = null;
            if (isHotspot)
            {
                downloadUsed = TryLong(GetWord(sentence, "bytes-out"));
                uploadUsed = TryLong(GetWord(sentence, "bytes-in"));
                calculatedStatus = expired ? VoucherStatus.Expired : VoucherStatus.Unused;
            }
            else
            {
                downloadUsed = TryLong(GetWord(sentence, "download-used"));
                uploadUsed = TryLong(GetWord(sentence, "upload-used"));
                var lastSeen = GetWord(sentence, "last-seen");
                var uptimeSecs = GetWord(sentence, "uptime-used");
                var hasUsage = (downloadUsed.HasValue && downloadUsed.Value > 0) ||
                               (uploadUsed.HasValue && uploadUsed.Value > 0) ||
                               (!string.IsNullOrEmpty(uptimeSecs) && uptimeSecs != "0s") ||
                               (!string.IsNullOrEmpty(lastSeen) && !string.Equals(lastSeen, "never", StringComparison.OrdinalIgnoreCase));
                calculatedStatus = hasUsage ? VoucherStatus.Used : VoucherStatus.Unused;
            }

            var isDisabled = string.Equals(GetWord(sentence, "disabled"), "true", StringComparison.OrdinalIgnoreCase);

            var voucher = new Voucher
            {
                Id = Guid.NewGuid(),
                Username = username,
                Password = password,
                Price = inferredPrice ?? 0, // EF Core price non-nullable default mappings
                ProfileName = profile,
                BatchId = batchId,
                CredentialMode = CredentialMode.UsernameAndPassword,
                Status = calculatedStatus,
                PrintStatus = VoucherPrintStatus.Reserved,
                AgentId = null,
                RouterId = routerId,
                VoucherSource = VoucherSource.ImportedFromRouter,
                ImportDate = DateTime.UtcNow,
                CreatedBy = "System Import",
                Comment = comment
            };

            // الـ SyncedAt والمعرف
            var mikroTikUserId = GetWord(sentence, ".id");
            if (!string.IsNullOrWhiteSpace(mikroTikUserId))
            {
                voucher.MarkAsSynced(mikroTikUserId);
            }

            chunk.Add(voucher);
            seenUsernames.Add(username);
            processedCount++;

            // حفظ الدفعة كل 2000 كرت
            if (chunk.Count >= 2000)
            {
                await SaveVoucherChunkInternalAsync(db, chunk, batchId, cancellationToken);
                chunk.Clear();
                progressCallback?.Invoke(processedCount, totalCount);
            }
        }

        // حفظ المتبقي
        if (chunk.Any())
        {
            await SaveVoucherChunkInternalAsync(db, chunk, batchId, cancellationToken);
            progressCallback?.Invoke(processedCount, totalCount);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SaveVoucherChunkInternalAsync(LuxCardDbContext db, List<Voucher> vouchers, Guid batchId, CancellationToken cancellationToken)
    {
        // إدراج الكروت
        db.Vouchers.AddRange(vouchers);
        
        // تحديث إجمالي الدفعة محلياً
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch != null)
        {
            batch.TotalCards += vouchers.Count;
        }

        await db.SaveChangesAsync(cancellationToken);

        // تفريغ تعقب الكائنات المضافة لتسريع الأداء وتفادي استهلاك الذاكرة
        db.ChangeTracker.Clear();
    }

    private async Task<int> GetRouterVoucherCountAsync(Guid routerId, CancellationToken cancellationToken)
    {
        try
        {
            await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
            var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
            if (router == null) return 0;

            var pass = "";
            if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
            {
                pass = _secureStorageService.Decrypt(router.EncryptedPassword);
            }

            return await Task.Run(() =>
            {
                using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                connection.SendTimeout = 8000;
                connection.ReceiveTimeout = 8000;
                connection.Open(router.Host, router.Username, pass);

                IEnumerable<ITikSentence> users;
                try
                {
                    users = connection.CreateCommandAndParameters("/tool/user-manager/user/print").ExecuteList().Cast<ITikSentence>();
                }
                catch
                {
                    try
                    {
                        users = connection.CreateCommandAndParameters("/user-manager/user/print").ExecuteList().Cast<ITikSentence>();
                    }
                    catch
                    {
                        users = connection.CreateCommandAndParameters("/ip/hotspot/user/print").ExecuteList().Cast<ITikSentence>();
                    }
                }
                return users.Count();
            }, cancellationToken);
        }
        catch
        {
            return 0; // تجاهل أخطاء الشبكة أثناء العد الافتراضي
        }
    }

    private static string? GetWord(ITikSentence sentence, string key)
    {
        if (sentence.Words.TryGetValue(key, out var val))
            return val;
        return null;
    }

    private static long? TryLong(string? val)
    {
        if (long.TryParse(val, out var res))
            return res;
        return null;
    }

    private static bool InferExpiredFromSentence(ITikSentence sentence, bool isHotspot)
    {
        if (isHotspot)
        {
            // Hotspot logic: checking limit-uptime vs uptime
            var limit = GetWord(sentence, "limit-uptime");
            var uptime = GetWord(sentence, "uptime");
            if (!string.IsNullOrEmpty(limit) && !string.IsNullOrEmpty(uptime) && limit == uptime)
                return true;
        }
        return false;
    }
}
