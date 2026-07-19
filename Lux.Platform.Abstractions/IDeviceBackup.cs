namespace Lux.Platform.Abstractions;

/// <summary>
/// العقد الأساسي لعمليات النسخ الاحتياطي للأجهزة
/// </summary>
public interface IDeviceBackup
{
    DeviceVendor SupportedVendor { get; }

    /// <summary>
    /// إنشاء نسخة احتياطية من إعدادات الجهاز
    /// </summary>
    /// <returns>مسار ملف النسخة الاحتياطية أو محتواه</returns>
    Task<string> CreateBackupAsync(IDevice device, string username, string password, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// استعادة إعدادات الجهاز من نسخة احتياطية
    /// </summary>
    Task RestoreBackupAsync(IDevice device, string username, string password, string backupFilePath, CancellationToken cancellationToken = default);
}
