using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Models.TemplateElements;

/// <summary>
/// عنصر رمز QR Code — يولد QR من قيمة Token أو بيانات ثابتة.
/// </summary>
public class QrCodeElement : TemplateElement
{
    /// <summary>
    /// رمز الحقل الذي سيتم تشفيره كـ QR.
    /// الأكثر شيوعاً: Username (رمز الدخول).
    /// </summary>
    public FieldToken DataToken { get; set; } = FieldToken.Username;

    /// <summary>
    /// بيانات ثابتة تُشفَّر في QR بدلاً من Token.
    /// إذا كانت غير null، تتقدم على DataToken.
    /// </summary>
    public string? StaticData { get; set; }

    /// <summary>مستوى تصحيح الأخطاء في QR Code</summary>
    public QrErrorCorrection ErrorCorrection { get; set; } = QrErrorCorrection.Medium;

    /// <summary>لون نقاط QR بصيغة HEX</summary>
    public string ForegroundColorHex { get; set; } = "#000000";

    /// <summary>لون خلفية QR بصيغة HEX</summary>
    public string BackgroundColorHex { get; set; } = "#FFFFFF";

    /// <summary>
    /// بادئة اختيارية تُضاف قبل قيمة Token.
    /// مثال: "http://hotspot.local/login?u=" + Username
    /// </summary>
    public string? UrlPrefix { get; set; }
}
