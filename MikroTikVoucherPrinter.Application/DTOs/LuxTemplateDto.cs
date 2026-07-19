using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// DTO خفيف لعرض القوالب في قائمة Template Library.
/// لا يحتوي على ElementsJson الكامل لتحسين الأداء.
/// </summary>
public sealed class LuxTemplateDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public TemplateCategory Category { get; init; }
    public TemplateOutputType OutputType { get; init; }
    public TemplateOrientation Orientation { get; init; }

    // أبعاد الكرت الأساسية
    public int CardsPerRow { get; init; }
    public int CardsPerColumn { get; init; }
    public float CardWidthMm { get; init; }
    public float CardHeightMm { get; init; }

    // حالة القالب
    public bool IsDefault { get; init; }
    public bool IsSystemTemplate { get; init; }
    public int Version { get; init; }

    // ربط الباقة
    public string? LinkedProfileName { get; init; }

    // عدد العناصر (محسوب من ElementsJson)
    public int ElementsCount { get; init; }

    // ══ Computed Display Properties ══

    public string CategoryDisplay => Category switch
    {
        TemplateCategory.VoucherCard => "بطاقة",
        TemplateCategory.Receipt     => "إيصال",
        TemplateCategory.Invoice     => "فاتورة",
        TemplateCategory.Report      => "تقرير",
        _                            => string.Empty,
    };

    public string OutputTypeDisplay => OutputType switch
    {
        TemplateOutputType.A4        => "A4",
        TemplateOutputType.A5        => "A5",
        TemplateOutputType.Thermal58 => "حراري 58mm",
        TemplateOutputType.Thermal80 => "حراري 80mm",
        TemplateOutputType.Card85x54 => "بطاقة",
        _                            => string.Empty,
    };

    public string GridSummary =>
        OutputType is TemplateOutputType.Thermal58 or TemplateOutputType.Thermal80
            ? "صفحة/كرت"
            : $"{CardsPerRow}×{CardsPerColumn}";

    public string SizeDisplay => OutputType switch
    {
        TemplateOutputType.A4        => "210×297mm",
        TemplateOutputType.A5        => "148×210mm",
        TemplateOutputType.Thermal58 => "58mm",
        TemplateOutputType.Thermal80 => "80mm",
        TemplateOutputType.Card85x54 => $"{CardWidthMm:F0}×{CardHeightMm:F0}mm",
        _                            => $"{CardWidthMm:F0}×{CardHeightMm:F0}mm",
    };

    public int CardsPerPage => CardsPerRow * CardsPerColumn;
}
