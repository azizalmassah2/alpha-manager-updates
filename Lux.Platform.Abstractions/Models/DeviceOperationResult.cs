using System;

namespace Lux.Platform.Abstractions.Models;

public class DeviceOperationResult
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
}
