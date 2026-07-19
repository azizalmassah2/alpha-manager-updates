using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Models;
using tik4net;
using tik4net.Api;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// خدمة الترخيص — تربط البرنامج بـ Serial Number لراوتر MikroTik واحد فقط.
/// يعمل من أي لابتوب طالما يتصل بنفس الراوتر المُرخَّص.
///
/// توافق كامل مع LicenseGenerator (YAZ-WART):
///   HardwareId في license.dat = SHA256(serial_number_للراوتر)
/// </summary>
public class LicenseService : ILicenseService
{
    private const string LicenseFileName  = "license.dat";
    private const string BackupFileName   = "license.backup";
    private const string RouterConfigFile = "router_cfg.dat";  // يُخزَّن بجانب license.dat
    private const string RegKeyPath       = @"Software\LuxCard";

    // ─── المفتاح العام RSA (مأخوذ من YAZ-WART\public_key.xml) ───────────────
    private const string PublicKeyXml =
        "<RSAKeyValue><Modulus>788Ha86wzO0AYs43DxUcX9JNIsu5m2UKDO7l6xWe6DyZwPPj8d32JDVR/skZc5ELwXl5Kmh902Dlz/mq/aA+GnlD5bbNuRSAMYvmEtvpPAW81qg8pwfNRbWV1ot0vL4w2UUJZesTxXvxdNSlCA+dzk2myLw+wRPq3wCH63yG2sUTni8McfoqxWw4GO2xQZiXEaUdLX2K6g+0TZeWYtHBAawp13uW74cHzEjpWpPF4b7YoPEn3JAPRhmaUTLIHs6aMBJXTKynatkwMYGMEtGjLXDA63hyx5UUw0QnkhtjtqkDGpfdt+J1GiAvl9i2xzyjKKFByllS1vgcPdcKmucGEKV7qVEhLl6IWJB48w5eIMEEvAElMSY3gKNF7vWdH9l8SUPEG9qJsU51QXcLdeUyliNpsezXOQ05y4TqFrNyHU+J+aP54jz4nIx41vQQlDxP0fyhbffzIWc+90lGjkVzW0va7Ci2pmIhnOSH44ef+Cyn0q8bX361V9JISh+4MxrzmijEqA2IBXcvwTQc+jdpI7BckTihENVOMy9hbA64XIW/Kdn7IlVIoVn+SVI8HI00WYveni8E5vhplXIbpGgNKadiuTQH9K+6nxU5tVu2WPXFrCS3PYQFhF4p8KTdndYo4SL7oDN6oxUevtPKj4m/y66qgNMRA5jLkRJKFoTlAC0=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    // ─── مفتاح AES لتشفير بيانات اتصال الراوتر ───────────────────────────────
    private static readonly byte[] RouterCfgKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("LuxCard-RouterCfg-EncKey-2026!"));
    private static readonly byte[] RouterCfgIV  = SHA256.HashData(
        Encoding.UTF8.GetBytes("LuxCard-RouterCfg-IV-Salt-2026!"))[..16];

    private readonly ILogger<LicenseService> _logger;
    private readonly IPublicKeyProvider _publicKeyProvider;

    public LicenseService(ILogger<LicenseService> logger, IPublicKeyProvider publicKeyProvider)
    {
        _logger = logger;
        _publicKeyProvider = publicKeyProvider;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ILicenseService
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public bool HasStoredLicense() => File.Exists(GetLicensePath());

    /// <inheritdoc/>
    public async Task<LicenseStatus> CheckLicenseAsync(
        string routerIp = "", string apiPassword = "", CancellationToken ct = default)
    {
        // إذا لم يُمرَّر IP، نقرأه من ملف الإعداد المحفوظ
        if (string.IsNullOrWhiteSpace(routerIp))
        {
            var cfg = LoadRouterConfig();
            if (cfg == null)
            {
                _logger.LogWarning("⚠️ [License] لا توجد بيانات اتصال راوتر محفوظة");
                // نسمح بالعمل مؤقتاً حتى يتصل المستخدم بالراوتر
                return LicenseStatus.CannotConnect;
            }
            routerIp    = cfg.Ip;
            apiPassword = cfg.Password;
        }

        return await Task.Run(() => ValidateLicense(routerIp, apiPassword), ct);
    }

    /// <inheritdoc/>
    public async Task<bool> ActivateAsync(
        string routerIp, string apiPassword, string licenseFilePath, CancellationToken ct = default)
    {
        string currentHwid = "";
        string json = "";
        LicenseInfo? license = null;
        try
        {
            if (!File.Exists(licenseFilePath))
            {
                SetDiagnostics("Corrupted License File", "", "", "", false, null, $"Selected license file does not exist: {licenseFilePath}");
                return false;
            }

            // قراءة وتحليل ملف الترخيص
            json = await File.ReadAllTextAsync(licenseFilePath, ct);
            try
            {
                license = JsonSerializer.Deserialize<LicenseInfo>(json);
            }
            catch (JsonException jex)
            {
                SetDiagnostics("Invalid JSON", json, "", "", false, null, $"JsonException during parsing: {jex.Message}");
                return false;
            }

            if (license == null)
            {
                SetDiagnostics("Corrupted License File", json, "", "", false, null, "Deserialization returned null.");
                return false;
            }

            if (string.IsNullOrEmpty(license.HardwareId) || string.IsNullOrEmpty(license.Signature))
            {
                SetDiagnostics("Missing Fields", json, license.HardwareId ?? "", "", false, license, "Missing HardwareId or Signature fields.");
                return false;
            }

            // قراءة Serial الراوتر والحصول على الـ HardwareId
            currentHwid = await GetHardwareIdAsync(routerIp, apiPassword, ct);
            if (string.IsNullOrEmpty(currentHwid))
            {
                SetDiagnostics("Router Connection Failure", json, license.HardwareId, "", false, license, $"Could not connect to router at {routerIp} to query Serial Number.");
                _logger.LogError("❌ [License] فشل التفعيل — لا يمكن قراءة Serial الراوتر من {IP}", routerIp);
                return false;
            }

            // التحقق من التوقيع RSA
            bool isSigValid = VerifySignature(license);
            if (!isSigValid)
            {
                SetDiagnostics("Signature Invalid", json, license.HardwareId, currentHwid, false, license, "RSA verification of the payload signature failed.");
                _logger.LogError("❌ [License] فشل التوقيع الرقمي");
                return false;
            }

            // التحقق من مطابقة الراوتر
            if (!MatchHardwareId(license, currentHwid))
            {
                SetDiagnostics("Hardware ID Mismatch", json, license.HardwareId, currentHwid, isSigValid, license, $"Hardware ID Mismatch. Current (Router): {currentHwid}, License: {license.HardwareId}");
                _logger.LogError("❌ [License] الراوتر المتصل لا يطابق الترخيص (Serial مختلف)");
                return false;
            }

            // التحقق من الصلاحية
            DateTime today = DateTime.Today;
            if (today > license.ExpiryDate)
            {
                int grace = license.GracePeriodDays - (today - license.ExpiryDate).Days;
                if (grace < 0)
                {
                    SetDiagnostics("Expired License", json, license.HardwareId, currentHwid, isSigValid, license, $"License expired on {license.ExpiryDate:yyyy-MM-dd}. Grace period also expired.");
                    return false;
                }
            }

            // حفظ license.dat في مجلد البرنامج
            string target = GetLicensePath();
            await File.WriteAllTextAsync(target, json, ct);

            // نسخة احتياطية
            await File.WriteAllTextAsync(GetBackupPath(), json, ct);

            // حفظ بيانات الراوتر مشفَّرةً لاستخدامها عند التحقق في كل تشغيل
            SaveRouterConfig(new RouterConfig { Ip = routerIp, Password = apiPassword });

            SetDiagnostics("Success", json, license.HardwareId, currentHwid, isSigValid, license, "Activation completed successfully!");

            _logger.LogInformation(
                "✅ [License] تم التفعيل — الراوتر: {IP} | HardwareId: {Hwid}",
                routerIp, currentHwid);
            return true;
        }
        catch (Exception ex)
        {
            SetDiagnostics("Corrupted License File", json, license?.HardwareId ?? "", currentHwid, false, license, $"ActivationException: {ex.Message}");
            _logger.LogError(ex, "❌ [License] استثناء أثناء التفعيل");
            return false;
        }
    }

    /// <inheritdoc/>
    public void Revoke()
    {
        try
        {
            foreach (var p in new[] { GetLicensePath(), GetBackupPath(), GetRouterConfigPath() })
                if (File.Exists(p)) File.Delete(p);

            Registry.CurrentUser.DeleteSubKey(RegKeyPath, throwOnMissingSubKey: false);
            _logger.LogInformation("🗑️ [License] تم إلغاء الترخيص");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ [License] خطأ أثناء إلغاء الترخيص");
        }
    }

    public class RouterSystemInfo
    {
        public string Model { get; set; } = "غير متوفر";
        public string Architecture { get; set; } = "غير متوفر";
        public string Version { get; set; } = "غير متوفر";
        public string SerialNumber { get; set; } = "غير متوفر";
        public string Identity { get; set; } = "غير متوفر";
    }

    public static async Task<RouterSystemInfo> GetRouterSystemInfoAsync(string ip, string password, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var info = new RouterSystemInfo();
            try
            {
                using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                connection.Open(ip, "admin", password);

                try
                {
                    var row = connection.CreateCommandAndParameters("/system/resource/print").ExecuteList().FirstOrDefault();
                    if (row != null)
                    {
                        var board = row.Words.FirstOrDefault(x => x.Key == "board-name").Value;
                        if (!string.IsNullOrEmpty(board)) info.Model = board;

                        var arch = row.Words.FirstOrDefault(x => x.Key == "architecture-name").Value;
                        if (!string.IsNullOrEmpty(arch)) info.Architecture = arch;

                        var ver = row.Words.FirstOrDefault(x => x.Key == "version").Value;
                        if (!string.IsNullOrEmpty(ver)) info.Version = ver;
                    }
                }
                catch { }

                var serial = ReadRouterboardSerial(connection);
                if (!string.IsNullOrEmpty(serial)) info.SerialNumber = serial;

                try
                {
                    var row = connection.CreateCommandAndParameters("/system/identity/print").ExecuteList().FirstOrDefault();
                    if (row != null)
                    {
                        var name = row.Words.FirstOrDefault(x => x.Key == "name").Value;
                        if (!string.IsNullOrEmpty(name)) info.Identity = name;
                    }
                }
                catch { }

                connection.Close();
            }
            catch { }
            return info;
        }, ct);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  قراءة Hardware ID من المايكروتيك
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// الاتصال بالراوتر وقراءة الـ Serial Number وإرجاع SHA256 منه.
    /// هذا هو الـ "Hardware ID" الذي يُدخَل في LicenseGenerator.
    /// </summary>
    public static async Task<string> GetHardwareIdAsync(
        string ip, string password, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                connection.Open(ip, "admin", password);

                var serial = ReadRouterboardSerial(connection);
                connection.Close();

                if (string.IsNullOrWhiteSpace(serial)) return string.Empty;

                // Hash بنفس الطريقة التي تُستخدم في HardwareIdProvider
                return HashSha256(serial.Trim().ToUpperInvariant());
            }
            catch { return string.Empty; }
        }, ct);
    }

    /// <summary>
    /// يُرجع الـ Serial Number الخام (غير مُهاش) من الراوتر — للعرض للمستخدم.
    /// </summary>
    public static async Task<string> GetRouterSerialRawAsync(
        string ip, string password, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                connection.Open(ip, "admin", password);
                var serial = ReadRouterboardSerial(connection);
                connection.Close();
                return serial ?? string.Empty;
            }
            catch { return string.Empty; }
        }, ct);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  التحقق الكامل (يُستدعى من App.xaml.cs)
    // ════════════════════════════════════════════════════════════════════════

    public LicenseStatus ValidateLicense(string routerIp, string routerPassword)
    {
        string licensePath = GetLicensePath();
        string backupPath  = GetBackupPath();
        LicenseInfo? license = null;
        string rawJson = "";
        string currentHwid = "";

        // 1. تحميل license.dat
        if (File.Exists(licensePath))
        {
            try
            {
                rawJson = File.ReadAllText(licensePath);
                license = JsonSerializer.Deserialize<LicenseInfo>(rawJson);
            }
            catch (JsonException jex)
            {
                SetDiagnostics("Invalid JSON", rawJson, "", "", false, null, $"JsonException: {jex.Message}");
            }
            catch (Exception ex)
            {
                SetDiagnostics("Corrupted License File", rawJson, "", "", false, null, $"ReadException: {ex.Message}");
            }
        }

        // 2. محاولة الاسترداد من النسخة الاحتياطية
        if (license == null && File.Exists(backupPath))
        {
            try
            {
                var backupJson = File.ReadAllText(backupPath);
                var backup = JsonSerializer.Deserialize<LicenseInfo>(backupJson);
                if (backup != null && VerifySignature(backup))
                {
                    File.WriteAllText(licensePath, backupJson);
                    license = backup;
                    rawJson = backupJson;
                    _logger.LogInformation("🔄 [License] استرداد من النسخة الاحتياطية");
                }
            }
            catch { }
        }

        if (license == null)
        {
            SetDiagnostics("Corrupted License File", rawJson, "", "", false, null, "No valid license file found on startup.");
            _logger.LogWarning("⚠️ [License] لا يوجد ملف ترخيص");
            return LicenseStatus.NotActivated;
        }

        if (string.IsNullOrEmpty(license.HardwareId) || string.IsNullOrEmpty(license.Signature))
        {
            SetDiagnostics("Missing Fields", rawJson, license.HardwareId ?? "", "", false, license, "Missing HardwareId or Signature fields.");
            return LicenseStatus.Corrupted;
        }

        // 3. تحقق PayloadHash
        if (license.PayloadHash != HashSha256(BuildPayloadString(license)))
        {
            SetDiagnostics("Corrupted License File", rawJson, license.HardwareId, "", VerifySignature(license), license, "PayloadHash checksum verification failed. File has been modified.");
            _logger.LogError("❌ [License] Checksum فاشل — ملف معدَّل");
            return LicenseStatus.Corrupted;
        }

        // 4. توقيع RSA
        bool isSigValid = VerifySignature(license);
        if (!isSigValid)
        {
            SetDiagnostics("Signature Invalid", rawJson, license.HardwareId, "", false, license, "RSA verification of the payload signature failed.");
            _logger.LogError("❌ [License] فشل التوقيع RSA");
            return LicenseStatus.Corrupted;
        }

        // 5. قراءة Serial الراوتر والتحقق
        try
        {
            using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
            connection.Open(routerIp, "admin", routerPassword);
            var serial = ReadRouterboardSerial(connection);
            connection.Close();

            if (string.IsNullOrWhiteSpace(serial))
            {
                SetDiagnostics("Missing Fields", rawJson, license.HardwareId, "", isSigValid, license, "Routerboard returned empty serial.");
                _logger.LogWarning("⚠️ [License] الراوتر لا يدعم قراءة الـ Serial");
                return LicenseStatus.CannotConnect;
            }

            currentHwid = HashSha256(serial.Trim().ToUpperInvariant());
        }
        catch (Exception ex)
        {
            SetDiagnostics("Router Connection Failure", rawJson, license.HardwareId, "", isSigValid, license, $"Router connection/query failed: {ex.Message}");
            _logger.LogWarning(ex, "⚠️ [License] لا يمكن الاتصال بالراوتر للتحقق — سيُسمح بالعمل مؤقتاً");
            return LicenseStatus.CannotConnect;
        }

        // 6. مقارنة الـ HardwareId
        if (!MatchHardwareId(license, currentHwid))
        {
            SetDiagnostics("Hardware ID Mismatch", rawJson, license.HardwareId, currentHwid, isSigValid, license, $"Hardware ID Mismatch. Current (Router): {currentHwid}, License: {license.HardwareId}");
            _logger.LogError("❌ [License] الراوتر المتصل ({Hwid}) لا يطابق الترخيص", currentHwid);
            return LicenseStatus.InvalidRouter;
        }

        // 7. هل مُلغى؟
        if (license.IsRevoked)
        {
            SetDiagnostics("Corrupted License File", rawJson, license.HardwareId, currentHwid, isSigValid, license, "License is marked as revoked.");
            _logger.LogError("❌ [License] الترخيص ملغى");
            return LicenseStatus.Corrupted;
        }

        // 8. انتهاء الصلاحية
        DateTime today = DateTime.Today;
        if (today > license.ExpiryDate)
        {
            int grace = license.GracePeriodDays - (today - license.ExpiryDate).Days;
            if (grace < 0)
            {
                SetDiagnostics("Expired License", rawJson, license.HardwareId, currentHwid, isSigValid, license, $"License expired on {license.ExpiryDate:yyyy-MM-dd}. Grace period also expired.");
                _logger.LogError("❌ [License] انتهت الصلاحية وفترة السماح");
                return LicenseStatus.Corrupted;
            }
            _logger.LogWarning("⚠️ [License] انتهت الصلاحية — أيام السماح المتبقية: {D}", grace);
        }

        // تحديث النسخة الاحتياطية
        try { File.WriteAllText(GetBackupPath(), File.ReadAllText(GetLicensePath())); } catch { }

        SetDiagnostics("Success", rawJson, license.HardwareId, currentHwid, isSigValid, license, "License is valid and verified successfully.");
        _logger.LogInformation("✅ [License] الترخيص صالح — العميل: {Name}", license.CustomerName);
        return LicenseStatus.Valid;
    }

    /// <inheritdoc/>
    public LicenseStatus ValidateLicenseOffline(string serialNumber)
    {
        string licensePath = GetLicensePath();
        string backupPath  = GetBackupPath();
        LicenseInfo? license = null;
        string rawJson = "";
        string currentHwid = "";

        // 1. تحميل license.dat
        if (File.Exists(licensePath))
        {
            try
            {
                rawJson = File.ReadAllText(licensePath);
                license = JsonSerializer.Deserialize<LicenseInfo>(rawJson);
            }
            catch (JsonException jex)
            {
                SetDiagnostics("Invalid JSON", rawJson, "", "", false, null, $"JsonException: {jex.Message}");
            }
            catch (Exception ex)
            {
                SetDiagnostics("Corrupted License File", rawJson, "", "", false, null, $"ReadException: {ex.Message}");
            }
        }

        // 2. محاولة الاسترداد من النسخة الاحتياطية
        if (license == null && File.Exists(backupPath))
        {
            try
            {
                var backupJson = File.ReadAllText(backupPath);
                var backup = JsonSerializer.Deserialize<LicenseInfo>(backupJson);
                if (backup != null && VerifySignature(backup))
                {
                    File.WriteAllText(licensePath, backupJson);
                    license = backup;
                    rawJson = backupJson;
                    _logger.LogInformation("🔄 [License] استرداد من النسخة الاحتياطية");
                }
            }
            catch { }
        }

        if (license == null)
        {
            SetDiagnostics("Corrupted License File", rawJson, "", "", false, null, "No valid license file found.");
            _logger.LogWarning("⚠️ [License] لا يوجد ملف ترخيص");
            return LicenseStatus.NotActivated;
        }

        if (string.IsNullOrEmpty(license.HardwareId) || string.IsNullOrEmpty(license.Signature))
        {
            SetDiagnostics("Missing Fields", rawJson, license.HardwareId ?? "", "", false, license, "Missing HardwareId or Signature fields.");
            return LicenseStatus.Corrupted;
        }

        // 3. تحقق PayloadHash
        if (license.PayloadHash != HashSha256(BuildPayloadString(license)))
        {
            SetDiagnostics("Corrupted License File", rawJson, license.HardwareId, "", VerifySignature(license), license, "PayloadHash checksum verification failed. File has been modified.");
            _logger.LogError("❌ [License] Checksum فاشل — ملف معدَّل");
            return LicenseStatus.Corrupted;
        }

        // 4. توقيع RSA
        bool isSigValid = VerifySignature(license);
        if (!isSigValid)
        {
            SetDiagnostics("Signature Invalid", rawJson, license.HardwareId, "", false, license, "RSA verification of the payload signature failed.");
            _logger.LogError("❌ [License] فشل التوقيع RSA");
            return LicenseStatus.Corrupted;
        }

        // 5. التحقق من السيريال
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            SetDiagnostics("Missing Fields", rawJson, license.HardwareId, "", isSigValid, license, "Router board returned empty serial.");
            return LicenseStatus.Corrupted;
        }

        currentHwid = HashSha256(serialNumber.Trim().ToUpperInvariant());

        // 6. مقارنة الـ HardwareId
        if (!MatchHardwareId(license, currentHwid))
        {
            SetDiagnostics("Hardware ID Mismatch", rawJson, license.HardwareId, currentHwid, isSigValid, license, $"Hardware ID Mismatch. Current (Router): {currentHwid}, License: {license.HardwareId}");
            _logger.LogError("❌ [License] الراوتر المتصل ({Hwid}) لا يطابق الترخيص", currentHwid);
            return LicenseStatus.InvalidRouter;
        }

        // 7. هل مُلغى؟
        if (license.IsRevoked)
        {
            SetDiagnostics("Corrupted License File", rawJson, license.HardwareId, currentHwid, isSigValid, license, "License is marked as revoked.");
            _logger.LogError("❌ [License] الترخيص ملغى");
            return LicenseStatus.Corrupted;
        }

        // 8. انتهاء الصلاحية
        DateTime today = DateTime.Today;
        if (today > license.ExpiryDate)
        {
            int grace = license.GracePeriodDays - (today - license.ExpiryDate).Days;
            if (grace < 0)
            {
                SetDiagnostics("Expired License", rawJson, license.HardwareId, currentHwid, isSigValid, license, $"License expired on {license.ExpiryDate:yyyy-MM-dd}. Grace period also expired.");
                _logger.LogError("❌ [License] انتهت الصلاحية وفترة السماح");
                return LicenseStatus.Corrupted;
            }
            _logger.LogWarning("⚠️ [License] انتهت الصلاحية — أيام السماح المتبقية: {D}", grace);
        }

        SetDiagnostics("Success", rawJson, license.HardwareId, currentHwid, isSigValid, license, "License is valid and verified successfully.");
        _logger.LogInformation("✅ [License] الترخيص صالح — العميل: {Name}", license.CustomerName);
        return LicenseStatus.Valid;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  دوال خاصة
    // ════════════════════════════════════════════════════════════════════════

    private static string? ReadRouterboardSerial(ITikConnection connection)
        => MikroTikRouterSerialReader.TryReadStableDeviceId(connection);

    private bool VerifySignature(LicenseInfo license)
    {
        try
        {
            string payload   = BuildPayloadString(license);
            byte[] data      = Encoding.UTF8.GetBytes(payload);
            byte[] sigBytes  = Convert.FromBase64String(license.Signature ?? string.Empty);
            
            if (!_publicKeyProvider.VerifyPublicKeyIntegrity())
            {
                _logger.LogError("❌ [Security] تم كشف تلاعب أو استبدال بالمفتاح العام للترخيص!");
                return false;
            }

            return _publicKeyProvider.VerifySignature(data, sigBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ [License] VerifySignature exception");
            return false;
        }
    }

    private static bool MatchHardwareId(LicenseInfo license, string currentHwid)
        => string.Equals(license.HardwareId, currentHwid, StringComparison.OrdinalIgnoreCase);

    private static string BuildPayloadString(LicenseInfo l)
        => $"{l.LicenseVersion}|{l.KeyVersion}|{l.LicenseId}|{l.CustomerName}|{l.HardwareId}|" +
           $"{l.CpuIdHash}|{l.BoardSerialHash}|{l.DiskSerialHash}|{l.MachineGuidHash}|" +
           $"{l.IssueDate:yyyy-MM-dd}|{l.ExpiryDate:yyyy-MM-dd}|{l.OfflineDays}|" +
           $"{l.GracePeriodDays}|{l.LicenseType}|{l.IsRevoked}|" +
           $"{string.Join(",", l.Features ?? new())}|{l.Notes}";

    // ─── Router Config (مشفَّر بـ AES-256) ─────────────────────────────────────

    private static void SaveRouterConfig(RouterConfig cfg)
    {
        var json     = JsonSerializer.Serialize(cfg);
        var rawBytes = Encoding.UTF8.GetBytes(json);
        using var aes = Aes.Create();
        aes.Key = RouterCfgKey;
        aes.IV  = RouterCfgIV;
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            cs.Write(rawBytes, 0, rawBytes.Length);
        File.WriteAllBytes(GetRouterConfigPath(), ms.ToArray());
    }

    private static RouterConfig? LoadRouterConfig()
    {
        string path = GetRouterConfigPath();
        if (!File.Exists(path)) return null;
        try
        {
            var enc = File.ReadAllBytes(path);
            using var aes = Aes.Create();
            aes.Key = RouterCfgKey;
            aes.IV  = RouterCfgIV;
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                cs.Write(enc, 0, enc.Length);
            return JsonSerializer.Deserialize<RouterConfig>(Encoding.UTF8.GetString(ms.ToArray()));
        }
        catch { return null; }
    }

    // ─── مسارات الملفات ─────────────────────────────────────────────────────────

    private static string GetLicensePath()    => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFileName);
    private static string GetRouterConfigPath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RouterConfigFile);

    private static string GetBackupPath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LuxCard");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, BackupFileName);
    }

    internal static string HashSha256(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    private string _lastDiagnostics = "لا توجد تفاصيل تشخيصية بعد.";
    public string LastDiagnostics => _lastDiagnostics;

    private void SetDiagnostics(
        string reason, 
        string rawJson, 
        string licenseHwid, 
        string currentHwid, 
        bool isSigValid, 
        LicenseInfo? license, 
        string internalDetails)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🔍 [تفاصيل تشخيص ترخيص Alpha Manager]");
        sb.AppendLine($"1. سبب الفشل الفعلي: {reason}");
        sb.AppendLine($"2. معرف عتاد الراوتر الحالي (Current HWID): {(!string.IsNullOrEmpty(currentHwid) ? currentHwid : "فشل في القراءة/غير متوفر")}");
        sb.AppendLine($"3. معرف العتاد في الترخيص (License HWID): {(!string.IsNullOrEmpty(licenseHwid) ? licenseHwid : "غير متوفر")}");
        sb.AppendLine($"4. صحة التوقيع الرقمي (Signature Valid): {(isSigValid ? "صحيح (True)" : "غير صحيح (False)")}");
        
        if (license != null)
        {
            sb.AppendLine($"5. اسم العميل: {license.CustomerName}");
            sb.AppendLine($"6. تاريخ الانتهاء: {license.ExpiryDate:yyyy-MM-dd}");
            sb.AppendLine($"7. فترة السماح: {license.GracePeriodDays} أيام");
            sb.AppendLine($"8. حالة الإلغاء: {(license.IsRevoked ? "ملغى" : "نشط")}");
        }
        else
        {
            sb.AppendLine("5. بيانات الترخيص: فارغة (فشل تحليل JSON)");
        }
        
        sb.AppendLine($"9. تفاصيل الخطأ الداخلية: {internalDetails}");
        sb.AppendLine("--------------------------------------------------");

        _lastDiagnostics = sb.ToString();
        _logger.LogWarning("⚠️ [License Diagnostics] {Diagnostics}", _lastDiagnostics);
    }

    // ─── Models داخلية ────────────────────────────────────────────────────────

    private sealed class RouterConfig
    {
        public string Ip       { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
