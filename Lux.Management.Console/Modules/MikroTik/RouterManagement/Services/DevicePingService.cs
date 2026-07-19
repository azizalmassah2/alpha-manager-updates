using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

public class DevicePingService : IDevicePingService
{
    public async Task<(bool IsReachable, double LatencyMs)> PingAsync(string ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ip)) return (false, -1);
        
        var targetIp = ip.Contains("/") ? ip.Split('/')[0] : ip;

        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(targetIp, 1000);
            return reply.Status == IPStatus.Success
                ? (true, reply.RoundtripTime)
                : (false, -1);
        }
        catch
        {
            return (false, -1);
        }
    }

    public async IAsyncEnumerable<DevicePingResult> PingBatchAsync(IEnumerable<string> ips, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var ip in ips)
        {
            if (ct.IsCancellationRequested) yield break;
            var (isReachable, latencyMs) = await PingAsync(ip, ct);
            yield return new DevicePingResult
            {
                IpAddress = ip,
                IsReachable = isReachable,
                LatencyMs = latencyMs
            };
        }
    }
}
