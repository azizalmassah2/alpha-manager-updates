using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LicenseGenerator
{
    public partial class MainWindow : Window
    {
        private string _cpuIdHash = string.Empty;
        private string _boardSerialHash = string.Empty;
        private string _diskSerialHash = string.Empty;
        private string _machineGuidHash = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDefaults();
            VerifyAuditLogIntegrity();
        }

        private void InitializeDefaults()
        {
            CbLicenseType.SelectedIndex = 0; // Trial
            TxtLicenseId.Text = $"LIC-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            TxtVersion.Text = "1";
            TxtKeyVersion.Text = "1";
            
            // Default check features
            ChkFeatureBatch.IsChecked = true;
            ChkFeatureLogs.IsChecked = true;
            ChkFeatureMulti.IsChecked = false;
        }

        private void CbLicenseType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpExpiryDate == null || TxtGracePeriod == null || TxtOfflineDays == null) return;

            DateTime today = DateTime.Today;
            var item = (ComboBoxItem)CbLicenseType.SelectedItem;
            string type = item.Content.ToString() ?? "";

            if (type.Contains("Trial"))
            {
                DpExpiryDate.SelectedDate = today.AddDays(7);
                TxtGracePeriod.Text = "2";
                TxtOfflineDays.Text = "7";
            }
            else if (type.Contains("Monthly"))
            {
                DpExpiryDate.SelectedDate = today.AddMonths(1);
                TxtGracePeriod.Text = "7";
                TxtOfflineDays.Text = "30";
            }
            else if (type.Contains("Yearly"))
            {
                DpExpiryDate.SelectedDate = today.AddYears(1);
                TxtGracePeriod.Text = "14";
                TxtOfflineDays.Text = "365";
            }
            else if (type.Contains("Lifetime"))
            {
                DpExpiryDate.SelectedDate = new DateTime(9999, 12, 31);
                TxtGracePeriod.Text = "0";
                TxtOfflineDays.Text = "99999";
            }
            else // Custom
            {
                DpExpiryDate.SelectedDate = today.AddDays(30);
                TxtGracePeriod.Text = "7";
                TxtOfflineDays.Text = "30";
            }
        }

        private void ImportRequest_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Activation Requests (activation_request.txt)|activation_request.txt;*.txt|All Files (*.*)|*.*",
                Title = "استيراد ملف طلب التفعيل"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(ofd.FileName);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("Customer Name:"))
                        {
                            TxtCustomerName.Text = line.Substring("Customer Name:".Length).Trim();
                        }
                        else if (line.StartsWith("Hardware ID:"))
                        {
                            TxtHardwareId.Text = line.Substring("Hardware ID:".Length).Trim();
                        }
                        else if (line.StartsWith("CPU ID Hash:"))
                        {
                            _cpuIdHash = line.Substring("CPU ID Hash:".Length).Trim();
                        }
                        else if (line.StartsWith("Board Serial Hash:"))
                        {
                            _boardSerialHash = line.Substring("Board Serial Hash:".Length).Trim();
                        }
                        else if (line.StartsWith("Disk Serial Hash:"))
                        {
                            _diskSerialHash = line.Substring("Disk Serial Hash:".Length).Trim();
                        }
                        else if (line.StartsWith("MachineGuid Hash:"))
                        {
                            _machineGuidHash = line.Substring("MachineGuid Hash:".Length).Trim();
                        }
                    }

                    MessageBox.Show("تم استيراد بيانات طلب التفعيل وبصمات الأجهزة الفرعية بنجاح!", 
                                    "نجاح الاستيراد", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل تحليل ملف الاستيراد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCustomerName.Text))
            {
                MessageBox.Show("يرجى إدخال اسم العميل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtHardwareId.Text))
            {
                MessageBox.Show("يرجى إدخال معرّف الجهاز (Hardware ID).", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (DpExpiryDate.SelectedDate == null)
            {
                MessageBox.Show("يرجى تحديد تاريخ انتهاء الصلاحية.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sfd = new SaveFileDialog
            {
                FileName = "license.dat",
                Filter = "License Files (license.dat)|license.dat;*.dat;*.json",
                Title = "حفظ ملف الترخيص المولد"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    int version = int.Parse(TxtVersion.Text);
                    int keyVer = int.Parse(TxtKeyVersion.Text);
                    string licenseId = TxtLicenseId.Text;
                    string customerName = TxtCustomerName.Text;
                    string hwid = TxtHardwareId.Text;
                    
                    DateTime issueDate = DateTime.Today;
                    DateTime expiryDate = DpExpiryDate.SelectedDate.Value;
                    
                    int offlineDays = int.Parse(TxtOfflineDays.Text);
                    int graceDays = int.Parse(TxtGracePeriod.Text);
                    
                    var item = (ComboBoxItem)CbLicenseType.SelectedItem;
                    string licType = item.Content.ToString() ?? "Trial";
                    if (licType.Contains("("))
                    {
                        // Clean "Trial (Trial)" to "Trial"
                        licType = licType.Substring(licType.IndexOf('(') + 1).Replace(")", "").Trim();
                    }

                    bool isRevoked = ChkRevoked.IsChecked == true;
                    string notes = TxtNotes.Text;

                    var features = new List<string>();
                    if (ChkFeatureBatch.IsChecked == true) features.Add("batch_programming");
                    if (ChkFeatureLogs.IsChecked == true) features.Add("advanced_logs");
                    if (ChkFeatureMulti.IsChecked == true) features.Add("multi_device");

                    // 1. Calculate Payload String
                    string payload = $"{version}|{keyVer}|{licenseId}|{customerName}|{hwid}|{_cpuIdHash}|{_boardSerialHash}|{_diskSerialHash}|{_machineGuidHash}|{issueDate:yyyy-MM-dd}|{expiryDate:yyyy-MM-dd}|{offlineDays}|{graceDays}|{licType}|{isRevoked}|{string.Join(",", features)}|{notes}";
                    string payloadHash = HashSha256(payload);

                    // 2. Sign using private key XML loaded from memory
                    string sigBase64;
                    using (var rsa = RSA.Create())
                    {
                        rsa.FromXmlString(PasswordWindow.DecryptedPrivateKey);
                        byte[] dataBytes = Encoding.UTF8.GetBytes(payload);
                        byte[] sigBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        sigBase64 = Convert.ToBase64String(sigBytes);
                    }

                    // 3. Populate model
                    var license = new LicenseModel
                    {
                        LicenseVersion = version,
                        KeyVersion = keyVer,
                        LicenseId = licenseId,
                        CustomerName = customerName,
                        HardwareId = hwid,
                        CpuIdHash = _cpuIdHash,
                        BoardSerialHash = _boardSerialHash,
                        DiskSerialHash = _diskSerialHash,
                        MachineGuidHash = _machineGuidHash,
                        IssueDate = issueDate,
                        ExpiryDate = expiryDate,
                        OfflineDays = offlineDays,
                        GracePeriodDays = graceDays,
                        LicenseType = licType,
                        IsRevoked = isRevoked,
                        Features = features,
                        Notes = notes,
                        PayloadHash = payloadHash,
                        Signature = sigBase64
                    };

                    string json = JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(sfd.FileName, json);

                    // 4. Save to secure audited history log (licenses.json)
                    WriteAuditLog(license);

                    MessageBox.Show("تم إنشاء وتوقيع ملف الترخيص (license.dat) بنجاح وحفظه في سجل التدقيق المالي الآمن للمنصة.", 
                                    "نجاح التوليد والتوقيع", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);

                    // Re-initialize ID for next license
                    InitializeDefaults();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ أثناء إنشاء الترخيص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void VerifyAuditLogIntegrity()
        {
            string auditPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "licenses.json");
            if (!File.Exists(auditPath)) return;

            try
            {
                string json = File.ReadAllText(auditPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var loaded = JsonSerializer.Deserialize<AuditLog>(json, options);
                
                if (loaded != null)
                {
                    string checkPayload = JsonSerializer.Serialize(loaded.Entries);
                    string checkHash = HashSha256(checkPayload);
                    if (loaded.AuditHash != checkHash)
                    {
                        MessageBox.Show("Audit log integrity violation detected.", 
                                        "Audit Log Protection", 
                                        MessageBoxButton.OK, 
                                        MessageBoxImage.Error);
                    }
                }
            }
            catch
            {
                MessageBox.Show("Audit log integrity violation detected.", 
                                "Audit Log Protection", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        private void WriteAuditLog(LicenseModel license)
        {
            string auditPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "licenses.json");
            AuditLog audit = new AuditLog();

            if (File.Exists(auditPath))
            {
                try
                {
                    string json = File.ReadAllText(auditPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var loaded = JsonSerializer.Deserialize<AuditLog>(json, options);
                    
                    if (loaded != null)
                    {
                        // Verify integrity
                        string checkPayload = JsonSerializer.Serialize(loaded.Entries);
                        string checkHash = HashSha256(checkPayload);
                        if (loaded.AuditHash != checkHash)
                        {
                            MessageBox.Show("Audit log integrity violation detected.", 
                                            "Audit Log Protection", 
                                            MessageBoxButton.OK, 
                                            MessageBoxImage.Error);
                        }
                        audit = loaded;
                    }
                }
                catch
                {
                    MessageBox.Show("Audit log integrity violation detected.", 
                                    "Audit Log Protection", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Error);
                }
            }

            var entry = new AuditEntry
            {
                LicenseId = license.LicenseId,
                CustomerName = license.CustomerName,
                HardwareId = license.HardwareId,
                LicenseType = license.LicenseType,
                IssueDate = license.IssueDate.ToString("yyyy-MM-dd"),
                ExpiryDate = license.ExpiryDate.ToString("yyyy-MM-dd")
            };

            audit.Entries.Add(entry);

            // Recompute cumulative hash
            string newPayload = JsonSerializer.Serialize(audit.Entries);
            audit.AuditHash = HashSha256(newPayload);

            string auditJson = JsonSerializer.Serialize(audit, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(auditPath, auditJson);
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

    public class AuditLog
    {
        [JsonPropertyName("audit_hash")]
        public string AuditHash { get; set; } = string.Empty;

        [JsonPropertyName("entries")]
        public List<AuditEntry> Entries { get; set; } = new();
    }

    public class AuditEntry
    {
        [JsonPropertyName("license_id")]
        public string LicenseId { get; set; } = string.Empty;

        [JsonPropertyName("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [JsonPropertyName("hardware_id")]
        public string HardwareId { get; set; } = string.Empty;

        [JsonPropertyName("license_type")]
        public string LicenseType { get; set; } = string.Empty;

        [JsonPropertyName("issue_date")]
        public string IssueDate { get; set; } = string.Empty;

        [JsonPropertyName("expiry_date")]
        public string ExpiryDate { get; set; } = string.Empty;
    }
}