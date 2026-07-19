namespace Lux.Platform.Abstractions.Models;

public sealed class FirmwareCompatibilityResult
{
    public bool IsCompatible { get; init; }
    public string? CurrentModel { get; init; }
    public string? FirmwareModel { get; init; }
    public string? Error { get; init; }
}
