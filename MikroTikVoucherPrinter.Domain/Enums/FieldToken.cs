namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// رموز الحقول الديناميكية المستخدمة في عناصر القوالب.
/// كل Token يمثل حقلاً يتم حله من بيانات الكرت أو السياق عند الطباعة.
/// </summary>
public enum FieldToken
{
    // ══ بيانات الكرت (8 حقول) ══
    /// <summary>اسم المستخدم / رمز الدخول</summary>
    Username = 0,
    /// <summary>كلمة السر</summary>
    Password = 1,
    /// <summary>السعر</summary>
    Price = 2,
    /// <summary>الحجم المسموح (بيانات / Quota)</summary>
    Quota = 3,
    /// <summary>مدة الاستخدام القصوى</summary>
    Duration = 4,
    /// <summary>مدة الصلاحية من تاريخ التفعيل</summary>
    Validity = 5,
    /// <summary>سرعة التحميل</summary>
    DownloadSpeed = 6,
    /// <summary>سرعة الرفع</summary>
    UploadSpeed = 7,

    // ══ بيانات الشبكة (3 حقول) ══
    /// <summary>اسم الشبكة / SSID</summary>
    NetworkName = 10,
    /// <summary>اسم الراوتر</summary>
    RouterName = 11,
    /// <summary>عنوان IP الراوتر</summary>
    RouterIp = 12,

    // ══ بيانات الوكيل (2 حقول) ══
    /// <summary>اسم الوكيل</summary>
    AgentName = 20,
    /// <summary>رقم هاتف الوكيل</summary>
    AgentPhone = 21,

    // ══ تواريخ وأوقات (3 حقول) ══
    /// <summary>تاريخ الطباعة</summary>
    PrintDate = 30,
    /// <summary>وقت الطباعة</summary>
    PrintTime = 31,
    /// <summary>تاريخ انتهاء الصلاحية (محسوب)</summary>
    ExpiryDate = 32,

    // ══ أرقام تسلسلية (4 حقول) ══
    /// <summary>رقم الكرت التسلسلي</summary>
    SerialNumber = 40,
    /// <summary>رقم الدفعة</summary>
    BatchNumber = 41,
    /// <summary>ترتيب الكرت في الدفعة (1, 2, 3...)</summary>
    CardIndex = 42,
    /// <summary>إجمالي عدد الكروت في الدفعة</summary>
    TotalCards = 43,

    // ══ مستقبلياً — فواتير وتقارير (3 حقول) ══
    /// <summary>اسم العميل</summary>
    CustomerName = 50,
    /// <summary>رقم الفاتورة</summary>
    InvoiceNumber = 51,
    /// <summary>المجموع الإجمالي</summary>
    Total = 52,
}
