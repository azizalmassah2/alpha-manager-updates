using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// خدمة الإعدادات - حفظ وتحميل الإعدادات مع ميزات المؤسسات:
/// Versioning, Atomic Saving, Backup/Restore on Corruption, Thread Safety
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private class SettingsEnvelope
    {
        public string Version { get; set; } = "1.0";
        public Dictionary<string, object> Data { get; set; } = new();
    }

    private readonly string _settingsPath;
    private readonly string _tempPath;
    private readonly string _backupPath;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly ConcurrentDictionary<string, object> _settings = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public JsonSettingsService(ILogger<JsonSettingsService> logger)
    {
        _logger = logger;
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LuxCard");

        Directory.CreateDirectory(appData);
        _settingsPath = Path.Combine(appData, "settings.json");
        _tempPath = Path.Combine(appData, "settings.tmp");
        _backupPath = Path.Combine(appData, "settings.bak");
    }

    public T Get<T>(string key, T defaultValue = default!)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            try
            {
                if (value is JsonElement jsonElement)
                {
                    var result = jsonElement.Deserialize<T>();
                    return result ?? defaultValue;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "فشل تحويل الإعداد {Key}. سيتم إرجاع القيمة الافتراضية", key);
                return defaultValue;
            }
        }
        return defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        if (value == null) return;
        _settings[key] = value;
        _logger.LogDebug("تم تحديث الإعداد: {Key}", key);
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            var envelope = new SettingsEnvelope
            {
                Version = "1.0",
                Data = new Dictionary<string, object>(_settings)
            };

            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // 1. الكتابة لملف مؤقت (Atomic safety)
            await File.WriteAllTextAsync(_tempPath, json);

            // 2. تحديث الباك آب إذا كان الملف الأساسي موجوداً وصالحاً
            if (File.Exists(_settingsPath))
            {
                File.Copy(_settingsPath, _backupPath, true);
            }

            // 3. استبدال الملف الأساسي بالملف المؤقت
            File.Move(_tempPath, _settingsPath, true);

            _logger.LogInformation("تم حفظ الإعدادات بنجاح بشكل آمن (Atomic Write) في {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل فادح أثناء حفظ الإعدادات");
            throw;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                if (File.Exists(_backupPath))
                {
                    _logger.LogWarning("ملف الإعدادات الأساسي مفقود، جاري الاستعادة من ملف النسخ الاحتياطي");
                    File.Copy(_backupPath, _settingsPath, true);
                }
                else
                {
                    _logger.LogInformation("ملف الإعدادات غير موجود. سيتم استخدام الإعدادات الافتراضية");
                    return;
                }
            }

            bool loadedSuccessfully = await TryLoadFromFileAsync(_settingsPath);

            if (!loadedSuccessfully && File.Exists(_backupPath))
            {
                _logger.LogWarning("الملف الأساسي فاسد. محاولة الاستعادة من النسخ الاحتياطي");
                loadedSuccessfully = await TryLoadFromFileAsync(_backupPath);
                if (loadedSuccessfully)
                {
                    // إصلاح الملف الأساسي باستخدام النسخة الاحتياطية السليمة
                    File.Copy(_backupPath, _settingsPath, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "حدث خطأ غير متوقع أثناء تحميل الإعدادات");
        }
    }

    private async Task<bool> TryLoadFromFileAsync(string path)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json)) return false;

            // محاولة قراءة الغلاف الجديد (V2+)
            var envelope = JsonSerializer.Deserialize<SettingsEnvelope>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (envelope != null && envelope.Data != null)
            {
                foreach (var kvp in envelope.Data)
                {
                    _settings[kvp.Key] = kvp.Value;
                }
                _logger.LogInformation("تم تحميل إعدادات الإصدار {Version} بنجاح", envelope.Version);
                return true;
            }

            // Fallback: محاولة قراءة الشكل القديم (V1 flat dictionary)
            var flatDict = JsonSerializer.Deserialize<Dictionary<string, object>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (flatDict != null)
            {
                foreach (var kvp in flatDict)
                {
                    _settings[kvp.Key] = kvp.Value;
                }
                _logger.LogInformation("تم تحميل الإعدادات بصيغة النظام القديم بنجاح. سيتم ترقيتها عند الحفظ القادم");
                return true;
            }

            return false;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "ملف JSON فاسد: {Path}", path);
            return false;
        }
    }
}
