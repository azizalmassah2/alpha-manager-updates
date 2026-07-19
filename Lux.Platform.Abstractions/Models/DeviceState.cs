using System;

namespace Lux.Platform.Abstractions.Models;

public class DeviceState
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public string? FirmwareVersion { get; set; }
    public double? CpuUsage { get; set; }
    public double? MemoryUsage { get; set; }
    public int ActiveUsers { get; set; }
    public DeviceHealthStatus Health { get; set; }
}
