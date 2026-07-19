using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using OpenWrtProgrammerPro.Helpers;
using OpenWrtProgrammerPro.Models;
using OpenWrtProgrammerPro.Services.Interfaces;

namespace OpenWrtProgrammerPro.Services
{
    public class OfflineLicenseValidator : IOfflineLicenseValidator
    {
        private ILoggerService Logger => ServiceLocator.Instance.Resolve<ILoggerService>();

        private const string LicenseFileName = "license.dat";
        private const string BackupFileName = "license.backup";
        private const string RegKeyPath = @"Software\OpenWrtProgrammerPro";
        
        public LicenseModel? ActiveLicense { get; private set; }

        public string GetHardwareId()
        {
            return HardwareIdProvider.GetHardwareId();
        }

        public string GetOfflineStatePath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWrtProgrammerPro");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "runtime.dat");
        }

        private string GetLicensePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFileName);
        }

        private string GetBackupPath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenWrtProgrammerPro");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, BackupFileName);
        }

        public async Task<LicenseValidationResult> ValidateLicenseAsync()
        {
            await Task.CompletedTask;
            string hwid = GetHardwareId();
            
            #if DEBUG
            Logger.Log($"[LIC] Checking license. System Hardware ID: {hwid}");
            #else
            // No verbose startup logs in Release
            #endif

            // 1. Try to load license.dat
            string licensePath = GetLicensePath();
            string backupPath = GetBackupPath();
            LicenseModel? license = null;

            if (File.Exists(licensePath))
            {
                try
                {
                    string json = File.ReadAllText(licensePath);
                    license = JsonSerializer.Deserialize<LicenseModel>(json);
                }
                catch (Exception ex)
                {
                    #if DEBUG
                    Logger.LogError($"[LIC] Failed to parse license file: {ex.Message}");
                    #endif
                }
            }

            // 2. If main license is missing/corrupted, try backup
            if (license == null && File.Exists(backupPath))
            {
                try
                {
                    string json = File.ReadAllText(backupPath);
                    license = JsonSerializer.Deserialize<LicenseModel>(json);
                    
                    if (license != null && VerifyLicenseSignature(license) && MatchHardwareId(license, hwid))
                    {
                        // Restore main license from backup
                        File.WriteAllText(licensePath, json);
                        #if DEBUG
                        Logger.Log("[LIC] License backup recovered successfully.");
                        #endif
                    }
                    else
                    {
                        license = null;
                    }
                }
                catch (Exception ex)
                {
                    #if DEBUG
                    Logger.LogError($"[LIC] Failed to restore license from backup: {ex.Message}");
                    #endif
                }
            }

            if (license == null)
            {
                #if DEBUG
                Logger.LogError("[LIC] License Signature Validation Failed - License missing or corrupt.");
                #else
                Logger.LogError("License Invalid");
                #endif
                return new LicenseValidationResult { Status = LicenseStatus.Missing, ErrorMessage = "ملف الترخيص (license.dat) مفقود أو تالف." };
            }

            #if DEBUG
            Logger.Log("[LIC] License Loaded");
            #endif

            // 3. Verify Checksum (PayloadHash)
            string expectedPayload = GetPayloadString(license);
            string computedHash = HashSha256(expectedPayload);
            if (license.PayloadHash != computedHash)
            {
                #if DEBUG
                Logger.LogError("[LIC] License Signature Validation Failed - Checksum mismatch.");
                #else
                Logger.LogError("License Invalid");
                #endif
                return new LicenseValidationResult { Status = LicenseStatus.SignatureFailed, ErrorMessage = "تعديل غير مصرح به في حقول ملف الترخيص (فشل التحقق من المجموع التحققي)." };
            }

            // 4. Verify RSA Signature
            if (!VerifyLicenseSignature(license))
            {
                #if DEBUG
                Logger.LogError("[LIC] License Signature Validation Failed");
                #else
                Logger.LogError("License Invalid");
                #endif
                return new LicenseValidationResult { Status = LicenseStatus.SignatureFailed, ErrorMessage = "فشل التحقق من التوقيع الرقمي لملف الترخيص." };
            }

            // 5. Verify Hardware ID (Supports Fuzzy Hardware Match)
            if (!MatchHardwareId(license, hwid))
            {
                #if DEBUG
                Logger.LogError("[LIC] Hardware ID Mismatch");
                #else
                Logger.LogError("License Invalid");
                #endif
                return new LicenseValidationResult { Status = LicenseStatus.HardwareMismatch, ErrorMessage = "معرف الجهاز لا يطابق معرف ترخيص البرنامج." };
            }

            // 6. Verify Revocation
            if (license.IsRevoked)
            {
                #if DEBUG
                Logger.LogError("[LIC] License Revoked");
                #else
                Logger.LogError("License Invalid");
                #endif
                return new LicenseValidationResult { Status = LicenseStatus.Revoked, ErrorMessage = "تم إيقاف/إلغاء هذا الترخيص من قبل المطور." };
            }

            // 7. Verify Local State Dates (Anti-Rollback & Clock Jump Detection)
            var stateResult = ValidateLocalState(hwid);
            if (stateResult.Status != LicenseStatus.Valid)
            {
                #if !DEBUG
                Logger.LogError("License Invalid");
                #endif
                return stateResult;
            }

            // 8. Verify Expiry & Grace Period
            DateTime today = DateTime.Today;
            int remainingDays = (license.ExpiryDate - today).Days;

            if (today > license.ExpiryDate)
            {
                int remainingGrace = license.GracePeriodDays - (today - license.ExpiryDate).Days;
                if (remainingGrace < 0)
                {
                    #if DEBUG
                    Logger.LogError("[LIC] License Expired");
                    #else
                    Logger.LogError("License Expired");
                    #endif
                    return new LicenseValidationResult { Status = LicenseStatus.Expired, ErrorMessage = "انتهت صلاحية ترخيص البرنامج وفترة السماح المتاحة." };
                }
                
                ActiveLicense = license;
                #if DEBUG
                Logger.LogWarning($"[LIC] License expired. Grace period remaining: {remainingGrace} days.");
                #else
                Logger.LogWarning("License Expired");
                #endif
                return new LicenseValidationResult 
                { 
                    Status = LicenseStatus.Valid, 
                    RemainingDays = remainingDays, 
                    RemainingGraceDays = remainingGrace,
                    ErrorMessage = $"License expired. Grace period remaining: {remainingGrace} days.\nانتهت صلاحية الترخيص. فترة السماح المتبقية: {remainingGrace} أيام." 
                };
            }

            // Keep backup copy updated
            try
            {
                File.WriteAllText(backupPath, JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            ActiveLicense = license;
            #if DEBUG
            Logger.Log("[LIC] License Valid");
            #else
            Logger.Log("License Valid");
            #endif

            return new LicenseValidationResult 
            { 
                Status = LicenseStatus.Valid, 
                RemainingDays = remainingDays, 
                RemainingGraceDays = 0 
            };
        }

        public async Task<bool> LoadAndActivateLicenseAsync(string licenseFilePath)
        {
            try
            {
                if (!File.Exists(licenseFilePath)) return false;

                string json = await File.ReadAllTextAsync(licenseFilePath);
                var license = JsonSerializer.Deserialize<LicenseModel>(json);
                if (license == null) return false;

                string hwid = GetHardwareId();
                if (!VerifyLicenseSignature(license) || !MatchHardwareId(license, hwid))
                {
                    return false;
                }

                // Copy to application folder
                string targetPath = GetLicensePath();
                await File.WriteAllTextAsync(targetPath, json);
                
                // Copy to backup path
                string backupPath = GetBackupPath();
                await File.WriteAllTextAsync(backupPath, json);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool VerifyLicenseSignature(LicenseModel license)
        {
            try
            {
                string pubKeyXml = LoadPublicKeyFromResources(license.KeyVersion);
                if (string.IsNullOrEmpty(pubKeyXml)) return false;

                using var rsa = RSA.Create();
                rsa.FromXmlString(pubKeyXml);

                string payload = GetPayloadString(license);
                byte[] dataBytes = Encoding.UTF8.GetBytes(payload);
                byte[] sigBytes = Convert.FromBase64String(license.Signature);

                return rsa.VerifyData(dataBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }

        private string LoadPublicKeyFromResources(int keyVersion)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = $"OpenWrtProgrammerPro.Resources.public_key_v{keyVersion}.xml";
                
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) return string.Empty;

                using StreamReader reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool MatchHardwareId(LicenseModel license, string currentHwid)
        {
            if (license.HardwareId == currentHwid) return true;

            // Fuzzy Hardware ID check
            int score = 0;
            string curCpu = HardwareIdProvider.GetCpuIdHash();
            string curBoard = HardwareIdProvider.GetBoardSerialHash();
            string curDisk = HardwareIdProvider.GetDiskSerialHash();
            string curGuid = HardwareIdProvider.GetMachineGuidHash();

            if (!string.IsNullOrEmpty(license.CpuIdHash) && license.CpuIdHash == curCpu) score += 40;
            if (!string.IsNullOrEmpty(license.BoardSerialHash) && license.BoardSerialHash == curBoard) score += 40;
            if (!string.IsNullOrEmpty(license.DiskSerialHash) && license.DiskSerialHash == curDisk) score += 10;
            if (!string.IsNullOrEmpty(license.MachineGuidHash) && license.MachineGuidHash == curGuid) score += 10;

            #if DEBUG
            Logger.Log($"[LIC] Hardware fuzzy matching: CPU={license.CpuIdHash==curCpu}, Board={license.BoardSerialHash==curBoard}, Disk={license.DiskSerialHash==curDisk}, Guid={license.MachineGuidHash==curGuid}. Match Score={score}%");
            #endif

            return score >= 80;
        }

        private LicenseValidationResult ValidateLocalState(string hardwareId)
        {
            string statePath = GetOfflineStatePath();
            LocalState? fileState = LoadStateFromFile(statePath, hardwareId);
            LocalState? regState = LoadStateFromRegistry(hardwareId);

            DateTime now = DateTime.Now;

            // First install scenario (both missing)
            if (fileState == null && regState == null)
            {
                var newState = new LocalState
                {
                    InstallDate = now,
                    LastRunDate = now,
                    MaxSeenDate = now
                };
                SaveStateToFile(statePath, newState, hardwareId);
                SaveStateToRegistry(newState, hardwareId);
                return new LicenseValidationResult { Status = LicenseStatus.Valid };
            }

            // Integrity Mismatch: one is missing, or they differ
            if (fileState == null || regState == null)
            {
                #if DEBUG
                Logger.LogError("[LIC] Runtime State Mismatch - Missing one state storage location.");
                #endif
                return new LicenseValidationResult { Status = LicenseStatus.IntegrityViolation, ErrorMessage = "انتهاك لسلامة ترخيص البرنامج (فقدان ملفات التشغيل الأمنة)." };
            }

            if (Math.Abs((fileState.LastRunDate - regState.LastRunDate).TotalSeconds) > 60 ||
                Math.Abs((fileState.MaxSeenDate - regState.MaxSeenDate).TotalSeconds) > 60 ||
                Math.Abs((fileState.InstallDate - regState.InstallDate).TotalSeconds) > 60)
            {
                #if DEBUG
                Logger.LogError($"[LIC] Runtime State Mismatch - Value differences detected. FileLastRun={fileState.LastRunDate:s}, RegLastRun={regState.LastRunDate:s}");
                #endif
                return new LicenseValidationResult { Status = LicenseStatus.IntegrityViolation, ErrorMessage = "انتهاك لسلامة ترخيص البرنامج (عدم تطابق قيم التشغيل الثنائية)." };
            }

            // Rollback checks
            if (now < fileState.LastRunDate || now < fileState.MaxSeenDate)
            {
                #if DEBUG
                Logger.LogError("[LIC] System Time Manipulation Detected");
                #endif
                return new LicenseValidationResult { Status = LicenseStatus.TimeManipulation, ErrorMessage = "تم اكتشاف تلاعب بتاريخ النظام. يرجى ضبط الساعة." };
            }

            // Clock Jump Check (>365 days forward)
            if (now > fileState.LastRunDate.AddDays(365))
            {
                #if DEBUG
                Logger.LogWarning("[LIC] Abnormal system clock jump detected.");
                #endif
            }

            // Update dates
            fileState.LastRunDate = now;
            if (now > fileState.MaxSeenDate)
            {
                fileState.MaxSeenDate = now;
            }

            // Write back to both locations
            SaveStateToFile(statePath, fileState, hardwareId);
            SaveStateToRegistry(fileState, hardwareId);

            return new LicenseValidationResult { Status = LicenseStatus.Valid };
        }

        private string GetPayloadString(LicenseModel license)
        {
            return $"{license.LicenseVersion}|{license.KeyVersion}|{license.LicenseId}|{license.CustomerName}|{license.HardwareId}|{license.CpuIdHash}|{license.BoardSerialHash}|{license.DiskSerialHash}|{license.MachineGuidHash}|{license.IssueDate:yyyy-MM-dd}|{license.ExpiryDate:yyyy-MM-dd}|{license.OfflineDays}|{license.GracePeriodDays}|{license.LicenseType}|{license.IsRevoked}|{string.Join(",", license.Features ?? new())}|{license.Notes}";
        }

        private static byte[] GetEncryptionKey(string hardwareId)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(hardwareId));
        }

        private static byte[] GetEncryptionIV(string hardwareId)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(hardwareId + "_state_salt_value_2026"));
            byte[] iv = new byte[16];
            Array.Copy(hash, iv, 16);
            return iv;
        }

        private static void SaveStateToFile(string filePath, LocalState state, string hardwareId)
        {
            try
            {
                string json = JsonSerializer.Serialize(state);
                byte[] rawBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes;

                using (var aes = Aes.Create())
                {
                    aes.Key = GetEncryptionKey(hardwareId);
                    aes.IV = GetEncryptionIV(hardwareId);

                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(rawBytes, 0, rawBytes.Length);
                        }
                        encryptedBytes = ms.ToArray();
                    }
                }

                File.WriteAllBytes(filePath, encryptedBytes);
            }
            catch { }
        }

        private static LocalState? LoadStateFromFile(string filePath, string hardwareId)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(filePath);
                byte[] decryptedBytes;

                using (var aes = Aes.Create())
                {
                    aes.Key = GetEncryptionKey(hardwareId);
                    aes.IV = GetEncryptionIV(hardwareId);

                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(encryptedBytes, 0, encryptedBytes.Length);
                        }
                        decryptedBytes = ms.ToArray();
                    }
                }

                string json = Encoding.UTF8.GetString(decryptedBytes);
                return JsonSerializer.Deserialize<LocalState>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveStateToRegistry(LocalState state, string hardwareId)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath);
                if (key != null)
                {
                    string json = JsonSerializer.Serialize(state);
                    byte[] rawBytes = Encoding.UTF8.GetBytes(json);
                    byte[] encryptedBytes;

                    using (var aes = Aes.Create())
                    {
                        aes.Key = GetEncryptionKey(hardwareId);
                        aes.IV = GetEncryptionIV(hardwareId);

                        using (var ms = new MemoryStream())
                        {
                            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                            {
                                cs.Write(rawBytes, 0, rawBytes.Length);
                            }
                            encryptedBytes = ms.ToArray();
                        }
                    }

                    key.SetValue("State", Convert.ToBase64String(encryptedBytes), RegistryValueKind.String);
                }
            }
            catch { }
        }

        private static LocalState? LoadStateFromRegistry(string hardwareId)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath);
                if (key != null)
                {
                    var base64 = key.GetValue("State")?.ToString();
                    if (string.IsNullOrEmpty(base64)) return null;

                    byte[] encryptedBytes = Convert.FromBase64String(base64);
                    byte[] decryptedBytes;

                    using (var aes = Aes.Create())
                    {
                        aes.Key = GetEncryptionKey(hardwareId);
                        aes.IV = GetEncryptionIV(hardwareId);

                        using (var ms = new MemoryStream())
                        {
                            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                            {
                                cs.Write(encryptedBytes, 0, encryptedBytes.Length);
                            }
                            decryptedBytes = ms.ToArray();
                        }
                    }

                    string json = Encoding.UTF8.GetString(decryptedBytes);
                    return JsonSerializer.Deserialize<LocalState>(json);
                }
            }
            catch { }
            return null;
        }

        private static string HashSha256(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
