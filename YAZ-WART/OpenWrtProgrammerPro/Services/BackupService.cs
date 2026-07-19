using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Helpers;
using OpenWrtProgrammerPro.Models;
using OpenWrtProgrammerPro.Services.Interfaces;

namespace OpenWrtProgrammerPro.Services
{
    public class BackupService : IBackupService
    {
        private IUciService Uci => ServiceLocator.Instance.Resolve<IUciService>();
        private ILoggerService Logger => ServiceLocator.Instance.Resolve<ILoggerService>();

        private string BackupsDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");

        public BackupService()
        {
            if (!Directory.Exists(BackupsDirectory))
            {
                Directory.CreateDirectory(BackupsDirectory);
            }
        }

        public async Task<BackupData> CreateBackupAsync(string ip, string session, string deviceIp)
        {
            Logger.Log($"جاري أخذ نسخة احتياطية كاملة للإعدادات (system, network, wireless, dhcp) للجهاز {deviceIp}...");

            var backup = new BackupData
            {
                Timestamp = DateTime.Now,
                DeviceIp = deviceIp,
                Configs = new Dictionary<string, object>()
            };

            var configsToBackup = new[] { "system", "network", "wireless", "dhcp" };
            foreach (var config in configsToBackup)
            {
                try
                {
                    var dict = await Uci.GetConfigDictAsync(ip, session, config);
                    backup.Configs[config] = dict;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"لم نتمكن من نسخ ملف الإعدادات {config}: {ex.Message}");
                }
            }

            var filename = $"Backup_{backup.Timestamp:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(BackupsDirectory, filename);

            var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            Logger.LogSuccess($"[OK] Backup created successfully: {filename}");
            return backup;
        }

        public Task<List<string>> ListBackupsAsync()
        {
            var list = new List<string>();
            if (Directory.Exists(BackupsDirectory))
            {
                var files = Directory.GetFiles(BackupsDirectory, "Backup_*.json");
                foreach (var file in files)
                {
                    list.Add(Path.GetFileName(file));
                }
            }
            // Sort descending so newest is first
            list.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal));
            return Task.FromResult(list);
        }

        public async Task<BackupData> LoadBackupAsync(string filename)
        {
            var filePath = Path.Combine(BackupsDirectory, filename);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"الملف {filename} غير موجود في المجلد.");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var backup = JsonSerializer.Deserialize<BackupData>(json);
            if (backup == null)
            {
                throw new Exception("فشل تحليل محتوى النسخة الاحتياطية.");
            }

            return backup;
        }
    }
}
