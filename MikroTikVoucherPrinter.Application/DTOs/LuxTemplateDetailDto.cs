using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// DTO الكامل لعمليات إنشاء وتعديل القوالب.
/// يحتوي على جميع خصائص القالب بما فيها ElementsJson.
/// يُستخدم في نموذج التعديل (TemplateFormDialog) وخدمة محرك الطباعة.
/// </summary>
public sealed class LuxTemplateDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateCategory Category { get; set; } = TemplateCategory.VoucherCard;
    public TemplateOutputType OutputType { get; set; } = TemplateOutputType.A4;
    public TemplateOrientation Orientation { get; set; } = TemplateOrientation.Portrait;

    // Helper string properties for WPF ComboBox binding
    public string CategoryString
    {
        get => Category.ToString();
        set
        {
            if (Enum.TryParse<TemplateCategory>(value, out var parsed))
                Category = parsed;
        }
    }

    public string OutputTypeString
    {
        get => OutputType.ToString();
        set
        {
            if (Enum.TryParse<TemplateOutputType>(value, out var parsed))
                OutputType = parsed;
        }
    }

    // ══ أبعاد الصفحة ══
    public float PageWidthMm { get; set; } = 210f;
    public float PageHeightMm { get; set; } = 297f;

    // ══ شبكة الكروت ══
    public int CardsPerRow { get; set; } = 3;
    public int CardsPerColumn { get; set; } = 7;
    public float CardWidthMm { get; set; } = 63f;
    public float CardHeightMm { get; set; } = 38f;
    public float HorizontalGapMm { get; set; } = 0f;
    public float VerticalGapMm { get; set; } = 0f;

    // ══ الهوامش ══
    public float MarginTopMm { get; set; } = 5f;
    public float MarginBottomMm { get; set; } = 5f;
    public float MarginLeftMm { get; set; } = 5f;
    public float MarginRightMm { get; set; } = 5f;

    // ══ الخلفية ══
    public TemplateBackgroundType BackgroundType { get; set; } = TemplateBackgroundType.Solid;
    public string? BackgroundColorHex { get; set; } = "#FFFFFF";
    public string? BackgroundImagePath { get; set; }

    // ══ العناصر — القلب الأساسي ══
    public string ElementsJson { get; set; } = "[]";

    // ══ التصنيف والحالة ══
    public string? LinkedProfileName { get; set; }
    public int Version { get; set; } = 1;
    public bool IsSystemTemplate { get; set; } = false;
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// تحويل LuxTemplateDetailDto إلى LuxTemplateDto خفيف للعرض في القائمة.
    /// يحسب ElementsCount من طول ElementsJson.
    /// </summary>
    public LuxTemplateDto ToListDto(int elementsCount = 0) => new()
    {
        Id                = Id,
        Name              = Name,
        Description       = Description,
        Category          = Category,
        OutputType        = OutputType,
        Orientation       = Orientation,
        CardsPerRow       = CardsPerRow,
        CardsPerColumn    = CardsPerColumn,
        CardWidthMm       = CardWidthMm,
        CardHeightMm      = CardHeightMm,
        IsDefault         = IsDefault,
        IsSystemTemplate  = IsSystemTemplate,
        Version           = Version,
        LinkedProfileName = LinkedProfileName,
        ElementsCount     = elementsCount,
    };
}
