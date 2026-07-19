using System;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace MikroTikVoucherPrinter.Domain.Entities.Telemetry;

public class DeviceTelemetrySnapshot : BaseEntity
{
    public Guid RouterId { get; set; }
    public Router Router { get; set; } = null!;
    
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsed { get; set; }
    public long MemoryTotal { get; set; }
    public TimeSpan Uptime { get; set; }
    public double? Temperature { get; set; }
    
    public DeviceHealthStatus HealthStatus { get; set; }
}
