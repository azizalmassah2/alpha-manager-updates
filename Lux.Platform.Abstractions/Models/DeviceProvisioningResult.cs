using System;

namespace Lux.Platform.Abstractions.Models;

public class DeviceProvisioningResult
{
    public IDevice TargetDevice { get; set; } = default!;
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime FinishedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The backup taken before applying this template (if rollback was enabled/triggered)
    /// </summary>
    public DeviceBackup? RollbackBackup { get; set; }
}
