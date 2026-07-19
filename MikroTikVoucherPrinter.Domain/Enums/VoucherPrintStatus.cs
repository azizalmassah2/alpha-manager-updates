namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// حالة الكرت الفردي في دورة التوليد والطباعة
/// </summary>
public enum VoucherPrintStatus
{
    /// <summary>تم توليد الكرت محلياً وحجزه بقاعدة البيانات لمنع التكرار</summary>
    Reserved = 0,
    
    /// <summary>تمت مزامنة وإنشاء الحساب بنجاح على المايكروتك</summary>
    Synced = 1,
    
    /// <summary>تم تضمين الكرت بنجاح داخل ملف الـ PDF</summary>
    PdfGenerated = 2,
    
    /// <summary>تمت طباعة الكرت / إرساله للمسبع بنجاح</summary>
    Printed = 3,
    
    /// <summary>فشلت العملية في إحدى المراحل</summary>
    Failed = 4
}
