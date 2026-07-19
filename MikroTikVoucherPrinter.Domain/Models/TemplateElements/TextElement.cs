using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Models.TemplateElements;

/// <summary>
/// عنصر نص ثابت — محتواه لا يتغير بين الكروت.
/// </summary>
public class TextElement : TemplateElement
{
    /// <summary>النص الثابت المعروض</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>نوع الخط</summary>
    public string FontFamily { get; set; } = "Arial";

    /// <summary>حجم الخط بالنقاط (pt)</summary>
    public float FontSizePt { get; set; } = 10f;

    /// <summary>لون النص بصيغة HEX (#RRGGBB)</summary>
    public string ColorHex { get; set; } = "#000000";

    /// <summary>هل الخط غامق؟</summary>
    public bool IsBold { get; set; } = false;

    /// <summary>هل الخط مائل؟</summary>
    public bool IsItalic { get; set; } = false;

    /// <summary>المحاذاة الأفقية للنص</summary>
    public TextHorizontalAlignment Alignment { get; set; } = TextHorizontalAlignment.Right;
}
