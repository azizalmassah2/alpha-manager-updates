namespace Lux.Platform.Abstractions.Models;

public class FirmwareDeviceOperationResult : DeviceOperationResult
{
    public string? PreviousVersion { get; init; }
    public string? NewVersion { get; init; }
}
