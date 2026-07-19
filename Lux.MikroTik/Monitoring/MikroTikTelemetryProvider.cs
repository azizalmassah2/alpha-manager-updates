using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Interfaces;
using Lux.MikroTik.Models;
using Lux.MikroTik.Providers;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace Lux.MikroTik.Monitoring;

public class MikroTikTelemetryProvider : IMikroTikTelemetryProvider
{
    private readonly IRouterOsProvider _provider;

    public MikroTikTelemetryProvider(IRouterOsProvider provider)
    {
        _provider = provider;
    }

    public async Task<Result<DeviceTelemetry>> GetTelemetryAsync(IDevice device, string session, CancellationToken cancellationToken = default)
    {
        if (!_provider.IsConnected)
        {
            return Result<DeviceTelemetry>.Failure("Provider is not connected", ErrorType.ExternalService);
        }

        var snapshot = new MikroTikTelemetrySnapshot();

        // 1. /system/resource/print
        try
        {
            var resCommand = new MikroTikCommand { Command = "/system/resource/print" };
            var result = await _provider.ExecuteAsync(resCommand);
            if (result.IsSuccess && result.Value.RawData != null && result.Value.RawData.Count > 0)
            {
                var dict = result.Value.RawData.First();
                if (dict.TryGetValue("cpu-load", out var cpuLoad) && double.TryParse(cpuLoad, out var cpu))
                    snapshot.CpuUsage = cpu;

                if (dict.TryGetValue("total-memory", out var totalMem) && double.TryParse(totalMem, out var tm))
                    snapshot.MemoryTotal = tm / (1024 * 1024); // Convert to MB

                if (dict.TryGetValue("free-memory", out var freeMem) && double.TryParse(freeMem, out var fm))
                    snapshot.MemoryUsed = snapshot.MemoryTotal - (fm / (1024 * 1024));

                if (dict.TryGetValue("uptime", out var uptimeStr))
                    snapshot.Uptime = ParseMikroTikUptime(uptimeStr);
                    
                if (dict.TryGetValue("version", out var version))
                    snapshot.FirmwareVersion = version;
            }
        }
        catch { /* Partial failure allowed */ }

        // 2. /system/routerboard/print
        try
        {
            var rbCommand = new MikroTikCommand { Command = "/system/routerboard/print" };
            var rbResult = await _provider.ExecuteAsync(rbCommand);
            if (rbResult.IsSuccess && rbResult.Value.RawData != null && rbResult.Value.RawData.Count > 0)
            {
                var dict = rbResult.Value.RawData.First();
                if (dict.TryGetValue("board-name", out var boardName))
                    snapshot.BoardName = boardName;
            }
        }
        catch { /* Partial failure allowed */ }

        // 3. /interface/print count-only
        try
        {
            var ifCommand = new MikroTikCommand { Command = "/interface/print" };
            // Note: Since 'count-only' might need special parameter handling in tik4net depending on version, 
            // we will query list and count it. Or try count-only. Let's do normal print and count.
            var ifResult = await _provider.ExecuteAsync(ifCommand);
            if (ifResult.IsSuccess && ifResult.Value.RawData != null)
            {
                snapshot.InterfaceCount = ifResult.Value.RawData.Count;
            }
        }
        catch { /* Partial failure allowed */ }

        // 4. /ip/hotspot/active/print count-only
        try
        {
            var hsCommand = new MikroTikCommand { Command = "/ip/hotspot/active/print" };
            var hsResult = await _provider.ExecuteAsync(hsCommand);
            if (hsResult.IsSuccess && hsResult.Value.RawData != null)
            {
                snapshot.ActiveUsers = hsResult.Value.RawData.Count;
            }
        }
        catch { /* Partial failure allowed */ }

        // Map Snapshot to DeviceTelemetry
        var telemetry = new DeviceTelemetry
        {
            DeviceId = device.Id,
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = snapshot.CpuUsage,
            MemoryTotalMb = snapshot.MemoryTotal,
            MemoryUsedMb = snapshot.MemoryUsed,
            MemoryUsagePercent = snapshot.MemoryTotal > 0 ? (snapshot.MemoryUsed / snapshot.MemoryTotal) * 100 : 0,
            Uptime = snapshot.Uptime,
            FirmwareVersion = snapshot.FirmwareVersion,
            ConnectedClients = snapshot.ActiveUsers,
            Status = "Online"
        };

        return Result<DeviceTelemetry>.Success(telemetry);
    }

    private TimeSpan ParseMikroTikUptime(string uptime)
    {
        if (string.IsNullOrWhiteSpace(uptime)) return TimeSpan.Zero;
        
        // MikroTik uptime format e.g. "1w2d3h4m5s"
        int weeks = 0, days = 0, hours = 0, minutes = 0, seconds = 0;

        ExtractUnit(ref uptime, "w", out weeks);
        ExtractUnit(ref uptime, "d", out days);
        ExtractUnit(ref uptime, "h", out hours);
        ExtractUnit(ref uptime, "m", out minutes);
        ExtractUnit(ref uptime, "s", out seconds);

        return new TimeSpan((weeks * 7) + days, hours, minutes, seconds);
    }

    private void ExtractUnit(ref string timeStr, string unit, out int value)
    {
        value = 0;
        var idx = timeStr.IndexOf(unit);
        if (idx > -1)
        {
            int start = idx - 1;
            while (start >= 0 && char.IsDigit(timeStr[start]))
            {
                start--;
            }
            start++;
            if (int.TryParse(timeStr.Substring(start, idx - start), out var v))
            {
                value = v;
            }
            timeStr = timeStr.Remove(start, idx - start + 1);
        }
    }
}
