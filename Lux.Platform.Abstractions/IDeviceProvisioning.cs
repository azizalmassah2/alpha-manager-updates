namespace Lux.Platform.Abstractions;

/// <summary>
/// العقد الأساسي لبرمجة الأجهزة وتكوينها المبدئي (Provisioning)
/// </summary>
public interface IDeviceProvisioning
{
    DeviceVendor SupportedVendor { get; }

    /// <summary>
    /// تطبيق إعدادات شبكية على الجهاز (مثل تغيير IP، VLAN، إعدادات Wireless)
    /// </summary>
    Task ProvisionAsync(IDevice device, string username, string password, object provisioningConfig, IProgress<(int percent, string message)> progress, CancellationToken cancellationToken = default);
    
    // ملاحظة: 'provisioningConfig' سيُستبدل لاحقاً بـ DTO أو Interface محدد
}
