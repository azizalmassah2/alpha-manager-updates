using System;

namespace Lux.Platform.Abstractions.Models;

public sealed class FirmwareImage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Vendor { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string BoardName { get; init; } = string.Empty;
    public string? MinimumVersion { get; init; }
    public string? MaximumVersion { get; init; }
}
