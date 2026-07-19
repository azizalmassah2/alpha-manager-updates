namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// مصدر الكرت (هل تم توليده محلياً أم تم استيراده من راوتر خارجي مسبقاً)
/// </summary>
public enum VoucherSource
{
    /// <summary>
    /// تم توليده بالكامل داخل نظام Lux محلياً
    /// </summary>
    GeneratedByLux = 0,

    /// <summary>
    /// تم استيراده من الراوتر مسبقاً (كرت قديم)
    /// </summary>
    ImportedFromRouter = 1
}
