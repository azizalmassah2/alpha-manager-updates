using System;
using System.Collections.Generic;

namespace MikroTikVoucherPrinter.Application.DTOs.Platform;

public class DeviceDetailsDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? SerialNumber { get; set; }
    public string? SoftwareId { get; set; }
    public string? Identity { get; set; }
    public string? Model { get; set; }
    public string HealthState { get; set; } = "Unknown";
    public DateTime? LastSeenAt { get; set; }
    public bool IsMonitoring { get; set; }
    
    // Simplification for the UI
    public List<TelemetrySnapshotDto> RecentTelemetry { get; set; } = new();
}

public class TelemetrySnapshotDto
{
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsedPercent { get; set; }
}
