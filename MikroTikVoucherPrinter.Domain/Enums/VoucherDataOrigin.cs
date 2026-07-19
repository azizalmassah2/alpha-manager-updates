namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// مصدر سجل الكرت في واجهة الإدارة (محلي مقابل دمج مع الراوتر).
/// </summary>
public enum VoucherDataOrigin
{
    /// <summary>من قاعدة البيانات المحلية لهذا الراوتر فقط.</summary>
    Local = 0,

    /// <summary>موجود محلياً وظهر في جلب الراوتر.</summary>
    RouterMerged = 1,

    /// <summary>يظهر على الراوتر ولا يوجد له سجل محلي.</summary>
    RouterOnly = 2
}
