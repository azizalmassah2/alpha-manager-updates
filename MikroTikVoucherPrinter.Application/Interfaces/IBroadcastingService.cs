using MikroTikVoucherPrinter.Domain.Entities.Platform;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// واجهة خدمة أجهزة البث: كشف الأجهزة المحلية وإدارة الأجهزة المسجلة
/// </summary>
public interface IBroadcastingService
{
    // ── كشف الأجهزة المحلية (ARP / Ping Sweep) ───────────────────────────

    /// <summary>مسح الشبكة المحلية وإرجاع قائمة الأجهزة المكتشفة</summary>
    Task<List<DiscoveredNetworkDevice>> ScanLocalNetworkAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>بدء الاستماع المستمر والكشف التلقائي عن أجهزة الشبكة في الخلفية</summary>
    Task StartListeningAsync(
        Action<DiscoveredNetworkDevice> onDeviceUpdated,
        CancellationToken cancellationToken);

    // ── CRUD — الأجهزة المسجلة في قاعدة البيانات ─────────────────────────

    Task<List<BroadcastingDevice>> GetAllDevicesAsync();
    Task<BroadcastingDevice?> GetDeviceByIdAsync(Guid id);
    Task<BroadcastingDevice> AddDeviceAsync(BroadcastingDevice device);
    Task<BroadcastingDevice> UpdateDeviceAsync(BroadcastingDevice device);
    Task DeleteDeviceAsync(Guid id);
}

/// <summary>نموذج جهاز مكتشف في الشبكة المحلية عبر بروتوكولات كشف الجيران</summary>
public class DiscoveredNetworkDevice
{
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
    public string IPv6 { get; set; } = "no";
    public string Age { get; set; } = "0";
    public bool IsReachable { get; set; }
    public long PingMs { get; set; }
}
