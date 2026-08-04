namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// حالة المزامنة على مستوى الدفعة كاملة
/// </summary>
public enum BatchSyncStatus
{
    /// <summary>لم تبدأ أي مزامنة بعد</summary>
    Pending = 0,

    /// <summary>جاري المزامنة الآن</summary>
    InProgress = 1,

    /// <summary>اكتملت المزامنة بنجاح كامل</summary>
    Completed = 2,

    /// <summary>اكتملت جزئياً — بعض الكروت فشلت</summary>
    PartiallyFailed = 3,

    /// <summary>فشلت المزامنة كلياً</summary>
    Failed = 4,

    /// <summary>جاري إعادة محاولة الكروت الفاشلة</summary>
    Retrying = 5,

    /// <summary>توقفت في المنتصف — قابلة للاستكمال</summary>
    Paused = 6
}
