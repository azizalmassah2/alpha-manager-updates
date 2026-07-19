using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Models.TemplateElements;

/// <summary>
/// عنصر شكل هندسي (مستطيل، دائرة، مثلث، مستطيل مدور).
/// يُستخدم لإطارات الكروت أو أقسام التصميم الديكوري.
/// </summary>
public class ShapeElement : TemplateElement
{
    /// <summary>نوع الشكل الهندسي</summary>
    public ShapeType ShapeType { get; set; } = ShapeType.Rectangle;

    /// <summary>لون ملء الشكل بصيغة HEX — null يعني شفاف</summary>
    public string? FillColorHex { get; set; } = "#FFFFFF";

    /// <summary>لون حدود الشكل بصيغة HEX</summary>
    public string StrokeColorHex { get; set; } = "#000000";

    /// <summary>سماكة الحدود (mm) — 0 يعني بلا حدود</summary>
    public float StrokeWidth { get; set; } = 0.5f;

    /// <summary>نصف قطر الزوايا للمستطيل المدور (mm)</summary>
    public float CornerRadius { get; set; } = 0f;
}
