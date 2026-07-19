using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Models.TemplateElements;

/// <summary>
/// عنصر حقل ديناميكي — نواة نظام القوالب.
/// قيمته تُحل وقت الطباعة من بيانات الكرت أو السياق.
/// يرث من TextElement للحصول على خصائص الخط واللون.
/// </summary>
public class DynamicFieldElement : TextElement
{
    /// <summary>رمز الحقل الذي سيتم حله من بيانات الكرت</summary>
    public FieldToken Token { get; set; } = FieldToken.Username;

    /// <summary>
    /// تنسيق اختياري للقيمة المحلولة.
    /// مثال: "{0:N0}" للأرقام, "dd/MM/yyyy" للتواريخ.
    /// </summary>
    public string? FormatString { get; set; }

    /// <summary>نص احتياطي يظهر إذا كانت قيمة الحقل فارغة أو null</summary>
    public string FallbackText { get; set; } = "—";

    /// <summary>
    /// قيمة تجريبية لعرضها في وضع المعاينة (Preview Mode).
    /// لا تُستخدم عند الطباعة الفعلية.
    /// </summary>
    public string PreviewValue { get; set; } = "XXXX-XXXX";
}
