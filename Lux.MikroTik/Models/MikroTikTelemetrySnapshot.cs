using System;

namespace Lux.MikroTik.Models;

public class MikroTikTelemetrySnapshot
{
    public double CpuUsage { get; set; }
    public double MemoryTotal { get; set; }
    public double MemoryUsed { get; set; }
    public TimeSpan Uptime { get; set; }
    public string FirmwareVersion { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
    public int ActiveUsers { get; set; }
    public int InterfaceCount { get; set; }
}
