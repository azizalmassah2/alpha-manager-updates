using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Models.TemplateElements;

/// <summary>
/// عنصر خط فاصل — يُستخدم لتقسيم أقسام الكرت بصرياً.
/// الخط يمتد من (X, Y) بعرض Width أو ارتفاع Height حسب الاتجاه.
/// </summary>
public class LineElement : TemplateElement
{
    /// <summary>لون الخط بصيغة HEX</summary>
    public string ColorHex { get; set; } = "#CCCCCC";

    /// <summary>سماكة الخط (mm)</summary>
    public float Thickness { get; set; } = 0.5f;

    /// <summary>نمط الخط (صلب / متقطع / منقط)</summary>
    public LineStyle Style { get; set; } = LineStyle.Solid;

    /// <summary>هل الخط أفقي؟ إذا كان false، فهو عمودي</summary>
    public bool IsHorizontal { get; set; } = true;
}
