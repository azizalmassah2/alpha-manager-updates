using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Models;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// خدمة فحص التحديثات وتنزيلها من سيرفر GitHub.
///
/// تدعم بنية update.json الموسّعة:
///   - enabled              : تجاهل الإصدار إذا كانت false
///   - minimumSupportedVersion : إجبار التحديث إذا كان إصدار العميل أقل
///   - updateType           : تصنيف نوع التحديث
///   - mandatory            : منع تجاوز التحديث
///   - releaseNotes         : مصفوفة أو نص (backward compat)
///   - sha256               : محجوز للتحقق المستقبلي
/// </summary>
public class UpdateService : IUpdateService
{
    // ── رابط ملف التحديث الخام من GitHub ────────────────────────────────
    private const string UpdateManifestUrl =
        "https://raw.githubusercontent.com/azizalmassah2/alpha-manager-updates/main/update.json";
    // ────────────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // JsonStringEnumConverter يقرأ "optional" / "mandatory" / ...
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly ILogger<UpdateService> _logger;

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            // ── 1. جلب JSON من السيرفر مع إضافة بارامتر لمنع الكاش (Cache Busting) ──
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("User-Agent", "AlphaManager-Updater/2.0");

            var urlWithCacheBuster = $"{UpdateManifestUrl}?t={DateTime.UtcNow.Ticks}";
            var json = await http.GetStringAsync(urlWithCacheBuster, ct);

            // ── 2. تحليل JSON ─────────────────────────────────────────
            var info = JsonSerializer.Deserialize<UpdateInfo>(json, JsonOptions);

            if (info is null)
            {
                _logger.LogWarning("⚠️ [Update] ملف التحديث فارغ أو غير صالح");
                return UpdateCheckResult.NoUpdate;
            }

            // ── 3. فحص حقل enabled ────────────────────────────────────
            // false = هذا الإصدار مسحوب من الخدمة (إصدار معطوب)
            if (!info.Enabled)
            {
                _logger.LogInformation("ℹ️ [Update] الإصدار {V} معطّل من السيرفر — تجاهله",
                    info.Version);
                return UpdateCheckResult.NoUpdate;
            }

            // ── 4. تحديد إصدار العميل الحالي ─────────────────────────
            // GetEntryAssembly = EXE الرئيسي للبرنامج وليس مكتبة Infrastructure
            var current = Assembly.GetEntryAssembly()?.GetName().Version
                          ?? new Version(1, 0, 0);

            // ── 5. فحص minimumSupportedVersion ────────────────────────
            bool isVersionBlocked = false;

            if (!string.IsNullOrWhiteSpace(info.MinimumSupportedVersion)
                && Version.TryParse(info.MinimumSupportedVersion, out var minVersion)
                && current < minVersion)
            {
                isVersionBlocked = true;
                info.IsForcedByMinVersion = true;

                _logger.LogWarning(
                    "⛔ [Update] إصدار العميل {Current} أقل من الحد الأدنى المدعوم {Min} — التحديث إجباري",
                    current, minVersion);
            }

            // ── 6. فحص وجود إصدار أحدث ───────────────────────────────
            if (!info.IsNewerThan(current) && !isVersionBlocked)
            {
                _logger.LogDebug("✅ [Update] البرنامج محدَّث — الإصدار الحالي: {V}", current);
                return UpdateCheckResult.NoUpdate;
            }

            // ── 7. تسجيل نتيجة الفحص وإرجاعها ───────────────────────
            _logger.LogInformation(
                "🔄 [Update] تحديث {Type} متاح: {Remote} (الحالي: {Current}){Forced}",
                info.UpdateType,
                info.Version,
                current,
                isVersionBlocked ? " [إجباري بسبب minimumSupportedVersion]" : string.Empty);

            return UpdateCheckResult.Available(info, isVersionBlocked);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("⏱️ [Update] انتهت مهلة فحص التحديثات");
            return UpdateCheckResult.NoUpdate;
        }
        catch (Exception ex)
        {
            // الفشل هنا لا يوقف البرنامج — اتصال انقطع أو خطأ في التحليل
            _logger.LogWarning(ex, "⚠️ [Update] فشل فحص التحديثات — المتابعة بدون اتصال");
            return UpdateCheckResult.NoUpdate;
        }
    }

    /// <inheritdoc/>
    public async Task DownloadAndInstallAsync(
        UpdateInfo update,
        IProgress<int> progress,
        CancellationToken ct = default)
    {
        var fileName = $"AlphaManager_Update_{update.Version}.exe";
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);

        _logger.LogInformation("⬇️ [Update] بدء تنزيل: {Url} → {Path}",
            update.DownloadUrl, tempPath);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.Add("User-Agent", "AlphaManager-Updater/2.0");

        using var response = await http.GetAsync(
            update.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        // استخدام using blocks لضمان إغلاق وتحرير الملف فوراً قبل تشغيل العملية
        using (var stream = await response.Content.ReadAsStreamAsync(ct))
        using (var file = File.Create(tempPath))
        {
            var buffer     = new byte[8192];
            long downloaded = 0;
            int  read;

            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;

                if (totalBytes > 0)
                    progress.Report((int)(downloaded * 100 / totalBytes));
            }
        } // هنا يتم إغلاق الملف وتحرير قفل الويندوز عنه تماماً

        // التحقق من SHA256 لضمان سلامة التحديث وعدم العبث به
        if (!string.IsNullOrEmpty(update.Sha256))
        {
            _logger.LogInformation("🛡️ [Update] جاري التحقق من بصمة SHA-256 لملف التحديث...");
            VerifyHash(tempPath, update.Sha256);
        }

        _logger.LogInformation("✅ [Update] اكتمل التنزيل — تشغيل المثبّت: {Path}", tempPath);

        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
        Environment.Exit(0);
    }

    private void VerifyHash(string filePath, string expectedHash)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var fileStream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(fileStream);
            var computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            
            var cleanedExpected = expectedHash.Trim().ToLowerInvariant();
            if (computedHash != cleanedExpected)
            {
                throw new System.Security.Cryptography.CryptographicException(
                    $"SHA-256 verification failed. Expected: {cleanedExpected}, Computed: {computedHash}");
            }
            
            _logger.LogInformation("🛡️ [Update] تم التحقق من بصمة SHA-256 بنجاح للملف المحمل.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [Update] فشل التحقق من سلامة وصلاحية التحديث");
            
            // حذف الملف المؤقت التالف/المعدل فوراً للحماية
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch { }
            
            throw;
        }
    }
}
