using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Models.TemplateElements;

/// <summary>
/// عنصر باركود — يولد باركود من قيمة Token.
/// </summary>
public class BarcodeElement : TemplateElement
{
    /// <summary>رمز الحقل الذي سيتم تشفيره كباركود</summary>
    public FieldToken DataToken { get; set; } = FieldToken.Username;

    /// <summary>نوع الباركود</summary>
    public BarcodeType BarcodeType { get; set; } = BarcodeType.Code128;

    /// <summary>هل يتم عرض النص تحت الباركود؟</summary>
    public bool ShowText { get; set; } = true;

    /// <summary>لون الباركود بصيغة HEX</summary>
    public string ColorHex { get; set; } = "#000000";
}
