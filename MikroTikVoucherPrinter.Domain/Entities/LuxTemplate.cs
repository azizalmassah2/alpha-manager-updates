using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Models.TemplateElements;

namespace MikroTikVoucherPrinter.Domain.Entities;

/// <summary>
/// نموذج بيانات القالب الجديد في نظام Lux Template Engine.
/// موازٍ لـ <see cref="TemplateConfig"/> وليس بديلاً عنه في v1.0.
/// يدعم عناصر ديناميكية غير محدودة مخزنة كـ JSON.
/// </summary>
public class LuxTemplate : BaseEntity
{
    // ══ هوية القالب ══

    /// <summary>اسم القالب</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>وصف اختياري يوضح الغرض من القالب</summary>
    public string? Description { get; set; }

    /// <summary>تصنيف القالب حسب نوع المستند</summary>
    public TemplateCategory Category { get; set; } = TemplateCategory.VoucherCard;

    /// <summary>نوع ورق / حجم مخرج القالب</summary>
    public TemplateOutputType OutputType { get; set; } = TemplateOutputType.A4;

    /// <summary>اتجاه الصفحة (عمودي / أفقي)</summary>
    public TemplateOrientation Orientation { get; set; } = TemplateOrientation.Portrait;

    // ══ أبعاد الصفحة (بالملليمتر) ══

    /// <summary>عرض الصفحة الكاملة (mm)</summary>
    public float PageWidthMm { get; set; } = 210f;

    /// <summary>ارتفاع الصفحة الكاملة (mm)</summary>
    public float PageHeightMm { get; set; } = 297f;

    // ══ إعدادات شبكة الكروت (للطباعة المتعددة على صفحة واحدة) ══

    /// <summary>عدد الكروت في الصف الواحد</summary>
    public int CardsPerRow { get; set; } = 3;

    /// <summary>عدد الصفوف في الصفحة الواحدة</summary>
    public int CardsPerColumn { get; set; } = 7;

    /// <summary>عرض الكرت الواحد (mm)</summary>
    public float CardWidthMm { get; set; } = 63f;

    /// <summary>ارتفاع الكرت الواحد (mm)</summary>
    public float CardHeightMm { get; set; } = 38f;

    /// <summary>المسافة الفاصلة بين الكروت أفقياً (mm)</summary>
    public float HorizontalGapMm { get; set; } = 0f;

    /// <summary>المسافة الفاصلة بين الكروت عمودياً (mm)</summary>
    public float VerticalGapMm { get; set; } = 0f;

    // ══ هوامش الصفحة (mm) ══

    public float MarginTopMm { get; set; } = 5f;
    public float MarginBottomMm { get; set; } = 5f;
    public float MarginLeftMm { get; set; } = 5f;
    public float MarginRightMm { get; set; } = 5f;

    // ══ الخلفية ══

    /// <summary>نوع خلفية القالب</summary>
    public TemplateBackgroundType BackgroundType { get; set; } = TemplateBackgroundType.Solid;

    /// <summary>لون الخلفية بصيغة HEX (مستخدم عند BackgroundType=Solid)</summary>
    public string? BackgroundColorHex { get; set; } = "#FFFFFF";

    /// <summary>مسار صورة الخلفية المطلق (مستخدم عند BackgroundType=Image)</summary>
    public string? BackgroundImagePath { get; set; }

    // ══ العناصر (القلب الأساسي للقالب) ══

    /// <summary>
    /// قائمة عناصر القالب مخزنة كـ JSON Polymorphic.
    /// يُعدَّل مباشرة عبر خاصية Elements.
    /// </summary>
    public string ElementsJson { get; set; } = "[]";

    /// <summary>
    /// Helper Property لقراءة/كتابة عناصر القالب كـ List.
    /// غير مخزنة في قاعدة البيانات (NotMapped).
    /// </summary>
    [NotMapped]
    public List<TemplateElement> Elements
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<List<TemplateElement>>(
                    ElementsJson,
                    LuxTemplateJsonOptions.Default) ?? new List<TemplateElement>();
            }
            catch
            {
                return new List<TemplateElement>();
            }
        }
        set => ElementsJson = JsonSerializer.Serialize(value, LuxTemplateJsonOptions.Default);
    }

    // ══ الربط والتصنيف ══

    /// <summary>
    /// اسم الباقة (Profile) المرتبطة بهذا القالب — اختياري.
    /// إذا كان محدداً، يتم اقتراح هذا القالب تلقائياً عند طباعة كروت هذه الباقة.
    /// </summary>
    public string? LinkedProfileName { get; set; }

    // ══ الحالة والإعدادات ══

    /// <summary>رقم إصدار القالب — يزداد عند كل تعديل رئيسي</summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// قالب نظامي مدمج في التطبيق.
    /// القوالب النظامية لا تُحذف ولا تُعدَّل بالكامل.
    /// </summary>
    public bool IsSystemTemplate { get; set; } = false;

    /// <summary>
    /// القالب الافتراضي لهذه الفئة والنوع.
    /// يُستخدم تلقائياً عند عدم تحديد قالب معين.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    // ══ عزل البيانات ══

    /// <summary>معرف الراوتر — يضمن عزل القوالب بين الرواترات</summary>
    public Guid RouterId { get; set; }

    // ══ Computed Properties ══

    /// <summary>إجمالي عدد الكروت في الصفحة الواحدة</summary>
    [NotMapped]
    public int CardsPerPage => CardsPerRow * CardsPerColumn;

    /// <summary>وصف مختصر لحجم شبكة الكروت</summary>
    [NotMapped]
    public string GridSummary =>
        OutputType is TemplateOutputType.Thermal58 or TemplateOutputType.Thermal80
            ? "كرت واحد لكل صفحة"
            : $"{CardsPerRow}×{CardsPerColumn}";

    /// <summary>وصف مختصر للأبعاد</summary>
    [NotMapped]
    public string SizeDisplay => OutputType switch
    {
        TemplateOutputType.A4         => "A4 (210×297mm)",
        TemplateOutputType.A5         => "A5 (148×210mm)",
        TemplateOutputType.Thermal58  => "حراري 58mm",
        TemplateOutputType.Thermal80  => "حراري 80mm",
        TemplateOutputType.Card85x54  => $"{CardWidthMm}×{CardHeightMm}mm",
        _                             => $"{PageWidthMm}×{PageHeightMm}mm"
    };
}

/// <summary>
/// إعدادات JSON الموحدة لتسلسل/تحليل عناصر القالب مع دعم Polymorphism.
/// </summary>
public static class LuxTemplateJsonOptions
{
    private static JsonSerializerOptions? _default;

    public static JsonSerializerOptions Default => _default ??= new JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
