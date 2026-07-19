using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// طھظ…ط«ظٹظ„ ط®ظپظٹظپ ظ„ظ‚ط§ظ„ط¨ ط§ظ„ط·ط¨ط§ط¹ط© ظ„ظ„ط¹ط±ط¶ ظپظٹ ط§ظ„ظˆط§ط¬ظ‡ط§طھ ظˆط®ط¯ظ…ط© ط§ظ„ظ‚ظˆط§ظ„ط¨.
/// </summary>
public sealed class TemplateConfigDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TemplateType Kind { get; init; }
    public bool IsSystemTemplate { get; init; }
    public bool IsDefault { get; init; }
    public string? LegacyRendererKey { get; init; }
    public double? ThermalPrintableWidthMm { get; init; }

    public int Columns { get; init; }
    public int Rows { get; init; }
    public string? BackgroundImagePath { get; init; }

    public string KindDisplay => Kind switch
    {
        TemplateType.A4 => "A4",
        TemplateType.Thermal58 => "ط­ط±ط§ط±ظٹ 58",
        TemplateType.Thermal80 => "ط­ط±ط§ط±ظٹ 80",
        TemplateType.Custom => "ظ…ط®طµطµ",
        _ => ""
    };

    public string GridSummary => Kind is TemplateType.Thermal58 or TemplateType.Thermal80
        ? "طµظپط­ط© ظ„ظƒظ„ ظƒط±طھ"
        : $"{Columns} أ— {Rows}";
}
