namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// حالة الطباعة / PDF على مستوى الدفعة كاملة
/// </summary>
public enum BatchPrintStatus
{
    /// <summary>لم تبدأ عملية الطباعة بعد</summary>
    NotStarted = 0,

    /// <summary>جاري توليد PDF الآن</summary>
    Generating = 1,

    /// <summary>تم توليد PDF وحفظه بنجاح</summary>
    Generated = 2,

    /// <summary>تمت الطباعة الفعلية (فتح ملف PDF)</summary>
    Printed = 3,

    /// <summary>فشل توليد PDF</summary>
    Failed = 4
}
