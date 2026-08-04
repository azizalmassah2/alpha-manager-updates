namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// نوع نظام إدارة المستخدمين على الراوتر المكتشف.
/// يُستخدم من طبقة IMikroTikCommandProvider لتحديد أوامر RouterOS المناسبة.
/// </summary>
public enum RouterSystemType
{
    /// <summary>
    /// RouterOS v6 مع User Manager.
    /// مسارات الأوامر تبدأ بـ /tool/user-manager/...
    /// </summary>
    UserManagerV6,

    /// <summary>
    /// RouterOS v7 مع User Manager المُجدَّد.
    /// مسارات الأوامر تبدأ بـ /user-manager/...
    /// </summary>
    UserManagerV7,

    /// <summary>
    /// Hotspot فقط بدون User Manager.
    /// مسارات الأوامر تبدأ بـ /ip/hotspot/user/...
    /// </summary>
    Hotspot,

    /// <summary>
    /// لم يُكتشف النوع بعد أو فشل الاكتشاف.
    /// </summary>
    Unknown
}
