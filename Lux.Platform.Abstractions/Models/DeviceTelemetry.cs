using System;

namespace Lux.Platform.Abstractions.Models;

public class DeviceTelemetry
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double MemoryUsedMb { get; set; }
    public double MemoryTotalMb { get; set; }
    public TimeSpan Uptime { get; set; }
    public double Temperature { get; set; }
    public int ConnectedClients { get; set; }
    public double SignalStrength { get; set; }
    public double NoiseFloor { get; set; }
    public double TxRate { get; set; }
    public double RxRate { get; set; }
    public string FirmwareVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
