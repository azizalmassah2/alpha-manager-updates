namespace Lux.Platform.Abstractions;

/// <summary>
/// العقد الأساسي لمراقبة أداء الأجهزة واستهلاكها
/// </summary>
public interface IDeviceMonitor
{
    DeviceVendor SupportedVendor { get; }

    /// <summary>
    /// الحصول على إحصائيات الاستهلاك الحالية (CPU, RAM, Uptime, Bandwidth)
    /// </summary>
    Task<object> GetMetricsAsync(IDevice device, string username, string password, CancellationToken cancellationToken = default);
    
    // ملاحظة: سيتم استبدال 'object' بـ DTO مشترك للإحصائيات في المستقبل، 
    // ولكن نستخدم object مؤقتاً لتسهيل الدمج الأولي
}
