using System;

namespace Lux.Platform.Abstractions.Models.Monitoring;

public class DeviceMetric
{
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public int ActiveUsers { get; set; }
    public DeviceHealthStatus Health { get; set; }
}
