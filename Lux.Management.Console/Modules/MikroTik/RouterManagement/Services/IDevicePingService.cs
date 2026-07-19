using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

public class DevicePingResult
{
    public string IpAddress { get; set; } = string.Empty;
    public bool IsReachable { get; set; }
    public double LatencyMs { get; set; }
}

public interface IDevicePingService
{
    Task<(bool IsReachable, double LatencyMs)> PingAsync(string ip, CancellationToken ct = default);
    IAsyncEnumerable<DevicePingResult> PingBatchAsync(IEnumerable<string> ips, CancellationToken ct = default);
}
