namespace Lux.Platform.Abstractions;

/// <summary>
/// العقد الأساسي لإدارة الأجهزة واكتشافها
/// </summary>
public interface IDeviceManager
{
    DeviceVendor SupportedVendor { get; }

    /// <summary>
    /// اكتشاف تفاصيل الجهاز ومعلوماته الأساسية
    /// </summary>
    Task<IDevice> DiscoverDeviceAsync(string ipAddress, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// فحص حالة اتصال الجهاز
    /// </summary>
    Task<DeviceStatus> CheckStatusAsync(string ipAddress, CancellationToken cancellationToken = default);
}
