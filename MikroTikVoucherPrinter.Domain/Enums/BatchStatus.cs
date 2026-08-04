namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// حالة دورة حياة الدفعة الكاملة
/// </summary>
public enum BatchStatus
{
    /// <summary>تم إنشاء الدفعة ولم يبدأ أي عمل</summary>
    Created = 0,

    /// <summary>جاري توليد الكروت</summary>
    Generating = 1,

    /// <summary>اكتمل التوليد، في انتظار المزامنة</summary>
    Generated = 2,

    /// <summary>جاري المزامنة مع المايكروتك</summary>
    Syncing = 3,

    /// <summary>اكتملت المزامنة بالكامل</summary>
    Synced = 4,

    /// <summary>جاري توليد PDF أو الطباعة</summary>
    Printing = 5,

    /// <summary>اكتمل كل شيء — التوليد والمزامنة والطباعة</summary>
    Completed = 6,

    /// <summary>بعض العمليات فشلت لكن النظام يعمل جزئياً</summary>
    PartiallyFailed = 7,

    /// <summary>فشل كامل — لا كروت مزامنة أو طباعة</summary>
    Failed = 8,

    /// <summary>ألغاها المستخدم يدوياً</summary>
    Cancelled = 9,

    /// <summary>مؤرشفة — للقراءة فقط</summary>
    Archived = 10
}
