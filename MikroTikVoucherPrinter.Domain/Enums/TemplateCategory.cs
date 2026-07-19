namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// تصنيف القالب حسب نوع المستند الذي يُنتجه.
/// </summary>
public enum TemplateCategory
{
    /// <summary>بطاقة كرت إنترنت (الاستخدام الرئيسي)</summary>
    VoucherCard = 0,

    /// <summary>إيصال دفع أو تسليم</summary>
    Receipt = 1,

    /// <summary>فاتورة مالية</summary>
    Invoice = 2,

    /// <summary>تقرير (وكيل / عميل / إحصائي)</summary>
    Report = 3,
}
