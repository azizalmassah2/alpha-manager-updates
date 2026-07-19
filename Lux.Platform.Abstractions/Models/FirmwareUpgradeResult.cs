namespace Lux.Platform.Abstractions.Models;

public sealed class FirmwareUpgradeResult
{
    public bool Success { get; init; }
    public string? PreviousVersion { get; init; }
    public string? NewVersion { get; init; }
    public string? Error { get; init; }
}
