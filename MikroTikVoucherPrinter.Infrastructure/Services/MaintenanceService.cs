using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using Lux.Platform.Abstractions.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public enum MaintenanceScriptType
{
    QuotaCleanup,
    TimeCleanup,
    SessionsCleanup
}

/// <summary>
/// خدمة الصيانة لتنفيذ وجدولة اسكريبتات صيانة MikroTik،
/// وإعادة بناء وتنظيف قاعدة بيانات SQLite، وإعادة تشغيل التطبيق.
/// </summary>
public class MaintenanceService
{
    private readonly IMaintenanceScriptProviderFactory _scriptProviderFactory;
    private readonly IMikroTikCommandExecutor _commandExecutor;
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        IMaintenanceScriptProviderFactory scriptProviderFactory,
        IMikroTikCommandExecutor commandExecutor,
        IDbContextFactory<LuxCardDbContext> dbFactory,
        ILogger<MaintenanceService> logger)
    {
        _scriptProviderFactory = scriptProviderFactory;
        _commandExecutor = commandExecutor;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// يرسل الاسكريبت إلى /system/script وينشئ جدولة في /system/scheduler على الراوتر.
    /// يستخدم نهج upsert: تحديث إن وُجد، إضافة إن لم يوجد.
    /// </summary>
    public async Task<Result> ScheduleRouterScriptAsync(
        MaintenanceScriptType scriptType,
        string intervalText,
        CancellationToken ct = default)
    {
        try
        {
            var provider = await _scriptProviderFactory.GetProviderAsync(ct);

            string scriptName;
            string scriptSource;

            switch (scriptType)
            {
                case MaintenanceScriptType.QuotaCleanup:
                    scriptName = provider.CleanQuotaScriptName;
                    scriptSource = provider.BuildCleanQuotaScript();
                    break;
                case MaintenanceScriptType.TimeCleanup:
                    scriptName = provider.CleanTimeScriptName;
                    scriptSource = provider.BuildCleanTimeScript();
                    break;
                case MaintenanceScriptType.SessionsCleanup:
                    scriptName = provider.CleanSessionsScriptName;
                    scriptSource = provider.BuildCleanSessionsScript();
                    break;
                default:
                    return Result.Failure("نوع الاسكريبت غير معروف", ErrorType.Validation);
            }

            // 1. رفع أو تحديث الاسكريبت على الراوتر
            string? scriptId = await UpsertScriptAsync(scriptName, scriptSource, ct);
            if (scriptId == null)
            {
                return Result.Failure($"فشل في رفع الاسكريبت {scriptName} على الراوتر", ErrorType.ExternalService);
            }

            // 2. إنشاء أو تحديث الجدولة الدورية
            string scheduleName = $"{scriptName}_Sched";
            await UpsertSchedulerAsync(scheduleName, scriptName, intervalText, ct);

            _logger.LogInformation("Successfully scheduled maintenance script {Name} with interval {Int}", scriptName, intervalText);
            return Result.Success();
        }
        catch (NotSupportedException ex)
        {
            return Result.Failure($"النوع الحالي للراوتر لا يدعم اسكريبتات الصيانة: {ex.Message}", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling maintenance script");
            return Result.Failure($"حدث خطأ أثناء إرسال الجدولة: {ex.Message}", ErrorType.ExternalService);
        }
    }

    /// <summary>
    /// تنفيـذ اسكريبت الصيانة فوراً على الراوتر دون إضافة جدولة زمنيـة.
    /// يعدّ عدد المستخدمين قبل وبعد التنفيذ لإظهار عدد الكروت المُنظَّفة.
    /// </summary>
    public async Task<Result<string>> ExecuteRouterScriptImmediatelyAsync(
        MaintenanceScriptType scriptType,
        CancellationToken ct = default)
    {
        try
        {
            var provider = await _scriptProviderFactory.GetProviderAsync(ct);

            string scriptName;
            string scriptSource;
            string countPath; // مسار العناصر التي سيُنظَّفها الاسكريبت

            switch (scriptType)
            {
                case MaintenanceScriptType.QuotaCleanup:
                    scriptName = provider.CleanQuotaScriptName;
                    scriptSource = provider.BuildCleanQuotaScript();
                    countPath = "/ip/hotspot/user";
                    break;
                case MaintenanceScriptType.TimeCleanup:
                    scriptName = provider.CleanTimeScriptName;
                    scriptSource = provider.BuildCleanTimeScript();
                    countPath = "/ip/hotspot/user";
                    break;
                case MaintenanceScriptType.SessionsCleanup:
                    scriptName = provider.CleanSessionsScriptName;
                    scriptSource = provider.BuildCleanSessionsScript();
                    countPath = "/ip/hotspot/active";
                    break;
                default:
                    return Result<string>.Failure("نوع الاسكريبت غير معروف", ErrorType.Validation);
            }

            string tempScriptName = $"{scriptName}_RunOnce";

            // 1. عدّ العناصر قبل التنفيذ
            int countBefore = await CountRouterItemsAsync(countPath, ct);

            // 2. رفع الاسكريبت المؤقت على الراوتر (أو تحديثه إن كان موجوداً من تشغيل سابق)
            string? scriptId = await UpsertScriptAsync(tempScriptName, scriptSource, ct);
            if (scriptId == null)
            {
                return Result<string>.Failure("فشل في رفع الاسكريبت المؤقت على الراوتر", ErrorType.ExternalService);
            }

            // 3. تشغيل الاسكريبت فوراً عبر .id المباشر
            bool runSuccess = await RunScriptAsync(scriptId, ct);

            // 4. تنظيف وحذف الاسكريبت المؤقت (best-effort)
            await DeleteScriptAsync(scriptId, ct);

            if (!runSuccess)
            {
                return Result<string>.Failure("فشل تشغيل اسكريبت الصيانة فورياً على الراوتر", ErrorType.ExternalService);
            }

            // 5. عدّ العناصر بعد التنفيذ وحساب الفرق
            int countAfter = await CountRouterItemsAsync(countPath, ct);
            int cleaned = Math.Max(0, countBefore - countAfter);

            string summary = cleaned > 0
                ? $"تم تنظيف {cleaned} كرت/مستخدم على الراوتر\n(قبل: {countBefore} | بعد: {countAfter})"
                : $"اكتملت عملية الصيانة — لا يوجد عناصر تحتاج تنظيف\n(العدد الحالي: {countAfter})";

            _logger.LogInformation("Maintenance script {Name} executed. Before={Before} After={After} Cleaned={Cleaned}",
                scriptName, countBefore, countAfter, cleaned);

            return Result<string>.Success(summary);
        }
        catch (NotSupportedException ex)
        {
            return Result<string>.Failure($"النوع الحالي للراوتر لا يدعم اسكريبتات الصيانة: {ex.Message}", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing immediate maintenance script");
            return Result<string>.Failure($"حدث خطأ أثناء تنفيذ الصيانة الفورية: {ex.Message}", ErrorType.ExternalService);
        }
    }

    /// <summary>
    /// يعدّ عدد العناصر في مسار معين على الراوتر (مثال: /ip/hotspot/user)
    /// </summary>
    private async Task<int> CountRouterItemsAsync(string path, CancellationToken ct)
    {
        try
        {
            var cmd = new MikroTikCommand { Command = $"{path}/print" };
            var res = await _commandExecutor.ExecuteAsync(cmd, ct);
            return res.Success && res.RawData != null ? res.RawData.Count : 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CountRouterItemsAsync error for {Path}", path);
            return 0;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MikroTik Helper Methods — نهج Upsert الشامل
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// يجلب كل الاسكريبتات من الراوتر ويبحث عن اسكريبت بالاسم المطلوب في C#
    /// (بدون الاعتماد على فلتر ?name من RouterOS الذي قد لا يعمل عبر tik4net).
    /// </summary>
    private async Task<string?> FindScriptIdAsync(string scriptName, CancellationToken ct)
    {
        try
        {
            var printCmd = new MikroTikCommand { Command = "/system/script/print" };
            var res = await _commandExecutor.ExecuteAsync(printCmd, ct);

            if (!res.Success || res.RawData == null) return null;

            foreach (var item in res.RawData)
            {
                if (item.TryGetValue("name", out var name) &&
                    string.Equals(name, scriptName, StringComparison.OrdinalIgnoreCase) &&
                    item.TryGetValue(".id", out var id) &&
                    !string.IsNullOrEmpty(id))
                {
                    return id;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FindScriptIdAsync error for {Name}", scriptName);
        }
        return null;
    }

    /// <summary>
    /// يجلب كل الجدولات من الراوتر ويبحث بالاسم في C#.
    /// </summary>
    private async Task<string?> FindSchedulerIdAsync(string schedulerName, CancellationToken ct)
    {
        try
        {
            var printCmd = new MikroTikCommand { Command = "/system/scheduler/print" };
            var res = await _commandExecutor.ExecuteAsync(printCmd, ct);

            if (!res.Success || res.RawData == null) return null;

            foreach (var item in res.RawData)
            {
                if (item.TryGetValue("name", out var name) &&
                    string.Equals(name, schedulerName, StringComparison.OrdinalIgnoreCase) &&
                    item.TryGetValue(".id", out var id) &&
                    !string.IsNullOrEmpty(id))
                {
                    return id;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FindSchedulerIdAsync error for {Name}", schedulerName);
        }
        return null;
    }

    /// <summary>
    /// Upsert اسكريبت على الراوتر: إذا وُجد → يحدّثه، وإذا لم يوجد → يضيفه.
    /// يُعيد .id الاسكريبت، أو null عند الفشل.
    /// </summary>
    private async Task<string?> UpsertScriptAsync(string scriptName, string scriptSource, CancellationToken ct)
    {
        try
        {
            string? existingId = await FindScriptIdAsync(scriptName, ct);

            if (existingId != null)
            {
                // الاسكريبت موجود → تحديث المصدر فقط
                var setCmd = new MikroTikCommand
                {
                    Command = "/system/script/set",
                    Parameters = new Dictionary<string, string>
                    {
                        [".id"] = existingId,
                        ["source"] = scriptSource
                    }
                };
                await _commandExecutor.ExecuteAsync(setCmd, ct);
                _logger.LogDebug("Script {Name} updated (id={Id})", scriptName, existingId);
                return existingId;
            }
            else
            {
                // الاسكريبت غير موجود → إضافة جديد
                var addCmd = new MikroTikCommand
                {
                    Command = "/system/script/add",
                    Parameters = new Dictionary<string, string>
                    {
                        ["name"] = scriptName,
                        ["source"] = scriptSource,
                        ["dont-require-permissions"] = "no"
                    }
                };
                var addRes = await _commandExecutor.ExecuteAsync(addCmd, ct);
                if (!addRes.Success)
                {
                    _logger.LogWarning("UpsertScriptAsync add failed for {Name}: {Msg}", scriptName, addRes.Message);
                    return null;
                }

                // نحصل على .id من استجابة add (RouterOS يُعيده كـ ret)
                string? newId = ExtractIdFromAddResponse(addRes) ?? await FindScriptIdAsync(scriptName, ct);
                _logger.LogDebug("Script {Name} added (id={Id})", scriptName, newId);
                return newId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertScriptAsync failed for {Name}", scriptName);
            return null;
        }
    }

    /// <summary>
    /// Upsert جدولة على الراوتر: إذا وُجدت → تحديث، وإذا لم توجد → إضافة.
    /// </summary>
    private async Task UpsertSchedulerAsync(string schedulerName, string onEvent, string interval, CancellationToken ct)
    {
        try
        {
            string? existingId = await FindSchedulerIdAsync(schedulerName, ct);

            if (existingId != null)
            {
                // الجدولة موجودة → تحديث
                var setCmd = new MikroTikCommand
                {
                    Command = "/system/scheduler/set",
                    Parameters = new Dictionary<string, string>
                    {
                        [".id"] = existingId,
                        ["on-event"] = onEvent,
                        ["interval"] = interval
                    }
                };
                await _commandExecutor.ExecuteAsync(setCmd, ct);
                _logger.LogDebug("Scheduler {Name} updated (id={Id})", schedulerName, existingId);
            }
            else
            {
                // الجدولة غير موجودة → إضافة
                var addCmd = new MikroTikCommand
                {
                    Command = "/system/scheduler/add",
                    Parameters = new Dictionary<string, string>
                    {
                        ["name"] = schedulerName,
                        ["on-event"] = onEvent,
                        ["interval"] = interval,
                        ["start-time"] = "03:00:00"
                    }
                };
                await _commandExecutor.ExecuteAsync(addCmd, ct);
                _logger.LogDebug("Scheduler {Name} added", schedulerName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertSchedulerAsync failed for {Name}", schedulerName);
        }
    }

    /// <summary>
    /// تشغيل اسكريبت على الراوتر بواسطة .id المباشر.
    /// /run موجود في IsWriteCommand → tik4net يُرسل =.id=*X بالصيغة الصحيحة.
    /// </summary>
    private async Task<bool> RunScriptAsync(string scriptId, CancellationToken ct)
    {
        try
        {
            var runCmd = new MikroTikCommand
            {
                Command = "/system/script/run",
                Parameters = new Dictionary<string, string> { [".id"] = scriptId }
            };
            var res = await _commandExecutor.ExecuteAsync(runCmd, ct);
            if (res.Success)
                _logger.LogDebug("Script id={Id} executed successfully", scriptId);
            else
                _logger.LogWarning("Script id={Id} run returned failure: {Msg}", scriptId, res.Message);
            return res.Success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RunScriptAsync failed for id={Id}", scriptId);
            return false;
        }
    }

    /// <summary>
    /// حذف اسكريبت من الراوتر بواسطة .id المباشر (best-effort، يتجاهل الأخطاء).
    /// </summary>
    private async Task DeleteScriptAsync(string scriptId, CancellationToken ct)
    {
        try
        {
            var removeCmd = new MikroTikCommand
            {
                Command = "/system/script/remove",
                Parameters = new Dictionary<string, string> { [".id"] = scriptId }
            };
            await _commandExecutor.ExecuteAsync(removeCmd, ct);
            _logger.LogDebug("Script id={Id} removed after immediate execution", scriptId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DeleteScriptAsync ignored error for id={Id}", scriptId);
        }
    }

    /// <summary>
    /// يستخرج .id العنصر الجديد من استجابة أمر /add.
    /// RouterOS يُعيد .id كـ "ret" في !done sentence، أو كـ ".id" في !re sentence.
    /// </summary>
    private static string? ExtractIdFromAddResponse(MikroTikResponse response)
    {
        if (response?.RawData == null) return null;

        foreach (var dict in response.RawData)
        {
            // RouterOS v7: يُعيد .id مباشرة
            if (dict.TryGetValue(".id", out var dotId) && !string.IsNullOrEmpty(dotId))
                return dotId;

            // RouterOS v6/v7: يُعيد ret في !done
            if (dict.TryGetValue("ret", out var ret) && !string.IsNullOrEmpty(ret))
                return ret;
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Router & Database Operations
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// إعادة تشغيل راوتر المايكروتيك (/system/reboot)
    /// </summary>
    public async Task<Result> RebootRouterAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Sending reboot command to MikroTik router...");

            var rebootCmd = new MikroTikCommand { Command = "/system/reboot" };
            var res = await _commandExecutor.ExecuteAsync(rebootCmd, ct);

            if (res.Success)
                return Result.Success();

            return Result.Failure($"فشل إرسال أمر إعادة تشغيل الراوتر: {res.Message}", ErrorType.ExternalService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebooting MikroTik router");
            return Result.Failure($"حدث خطأ أثناء إرسال أمر إعادة تشغيل الراوتر: {ex.Message}", ErrorType.ExternalService);
        }
    }

    /// <summary>
    /// تشغيل تنظيف وصيانة قاعدة بيانات SQLite (VACUUM و REINDEX وحذف سجلات اللوج والجلسات المنتهية)
    /// </summary>
    public async Task<Result<string>> RebuildDatabaseAsync(
        bool cleanLogs = true,
        bool cleanSessions = true,
        bool doVacuum = true,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting SQLite database maintenance...");
            await using var dbContext = await _dbFactory.CreateDbContextAsync(ct);

            var details = new System.Text.StringBuilder();
            int totalDeleted = 0;

            if (cleanLogs)
            {
                int printJobEvents = await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM PrintJobEvents;", ct);
                int telemetry = await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM VlanTelemetryStates;", ct);
                int logsTotal = printJobEvents + telemetry;
                totalDeleted += logsTotal;
                details.AppendLine($"🧹 سجلات الأحداث واللوج: {logsTotal} سجل محذوف");
            }

            if (cleanSessions)
            {
                int vouchers = await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Vouchers WHERE IsDeleted = 1;", ct);
                totalDeleted += vouchers;
                details.AppendLine($"🗑️ الكروت المحذوفة (IsDeleted): {vouchers} كرت محذوف");
            }

            if (doVacuum)
            {
                await dbContext.Database.ExecuteSqlRawAsync("VACUUM;", ct);
                await dbContext.Database.ExecuteSqlRawAsync("REINDEX;", ct);
                details.AppendLine("🗄️ تم تقليص حجم الملف وإعادة بناء الفهارس (VACUUM & REINDEX)");
            }

            string summary = totalDeleted > 0
                ? $"إجمالي السجلات المحذوفة: {totalDeleted} سجل\n\n{details}"
                : $"لا توجد سجلات للحذف\n\n{details}";

            _logger.LogInformation("Database maintenance completed. Total deleted: {Total}", totalDeleted);
            return Result<string>.Success(summary.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding SQLite database");
            return Result<string>.Failure($"فشل في صيانة وإعادة بناء قاعدة البيانات: {ex.Message}", ErrorType.Unexpected);
        }
    }

    /// <summary>
    /// إعادة تشغيل التطبيق
    /// </summary>
    public Result RestartApplication()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var fileName = currentProcess.MainModule?.FileName;

            if (!string.IsNullOrEmpty(fileName))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = true
                });

                Environment.Exit(0);
                return Result.Success();
            }

            return Result.Failure("تعذر تحديد مسار تشغيل التطبيق.", ErrorType.Unexpected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting application");
            return Result.Failure($"فشل إعادة تشغيل التطبيق: {ex.Message}", ErrorType.Unexpected);
        }
    }
}
