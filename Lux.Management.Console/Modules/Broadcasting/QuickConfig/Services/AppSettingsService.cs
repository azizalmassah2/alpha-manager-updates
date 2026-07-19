using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services
{
    /// <summary>
    /// خدمة حفظ واستعادة إعدادات التطبيق تلقائياً.
    /// يُحفظ الملف في: {AppBaseDir}/Data/appsettings.json
    /// </summary>
    public class AppSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Data", "appsettings.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public async Task<AppSettings> LoadAsync()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new AppSettings();

                var json = await File.ReadAllTextAsync(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public async Task SaveAsync(AppSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, JsonOpts);
                await File.WriteAllTextAsync(SettingsPath, json);
            }
            catch
            {
                // حفظ الإعدادات غير حرج — نتجاهل الأخطاء بصمت
            }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, JsonOpts);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // تجاهل الأخطاء
            }
        }
    }
}
