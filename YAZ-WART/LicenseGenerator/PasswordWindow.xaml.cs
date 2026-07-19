using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace LicenseGenerator
{
    public partial class PasswordWindow : Window
    {
        // Encrypted RSA Private Key XML (encrypted with AES-256 using Master Password 'mssara')
        private const string EncryptedPrivateKey = "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";

        public static string DecryptedPrivateKey { get; private set; } = string.Empty;

        public PasswordWindow()
        {
            InitializeComponent();
            
            string currentHwid = HardwareIdProvider.GetHardwareId();
            TxtHwid.Text = currentHwid;

            var allowedHwids = GetAuthorizedHardwareIds();

            if (!allowedHwids.Contains(currentHwid))
            {
                MessageBox.Show($"عذراً، هذا الجهاز غير مصرح له بتشغيل أداة توليد التراخيص.\n\nبصمة الجهاز الحالي:\n{currentHwid}", 
                                "جهاز غير مصرح به", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private HashSet<string> GetAuthorizedHardwareIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Hardcoded defaults to prevent lockouts and authorize base machines
            ids.Add("2F558B7FB5F3C748B53BAC56158832C00E6C2C081460588AB186F944DFB7BF49");
            ids.Add("9C7A08BD30AC400BB8D1070C42D4729EECCC70C82582E9786F903722268B549C");

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "developer_hardware_ids.json");
            
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var fileIds = JsonSerializer.Deserialize<List<string>>(json);
                    if (fileIds != null)
                    {
                        foreach (var id in fileIds)
                        {
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                ids.Add(id.Trim());
                            }
                        }
                    }
                }
                catch { }
            }
            else
            {
                // Write default config file if it does not exist
                try
                {
                    var fileIds = ids.ToList();
                    string json = JsonSerializer.Serialize(fileIds, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configPath, json);
                }
                catch { }
            }

            return ids;
        }

        private void Verify_Click(object sender, RoutedEventArgs e)
        {
            string password = TxtPassword.Password;
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("يرجى إدخال كلمة المرور الرئيسية.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Derive key and IV using PBKDF2
                byte[] salt = Encoding.UTF8.GetBytes("generator_key_salt_2026");
                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
                byte[] key = pbkdf2.GetBytes(32);
                byte[] iv = pbkdf2.GetBytes(16);

                byte[] cipherBytes = Convert.FromBase64String(EncryptedPrivateKey);
                byte[] plainBytes;

                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                        }
                        plainBytes = ms.ToArray();
                    }
                }

                string rsaXml = Encoding.UTF8.GetString(plainBytes);

                // Verify key is a valid RSA Private Key
                using (var rsa = RSA.Create())
                {
                    rsa.FromXmlString(rsaXml);
                }

                DecryptedPrivateKey = rsaXml;
                DialogResult = true;
                Close();
            }
            catch
            {
                MessageBox.Show("كلمة المرور الرئيسية غير صحيحة. فشل فك تشفير المفتاح الخاص.", 
                                "خطأ في التحقق", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
