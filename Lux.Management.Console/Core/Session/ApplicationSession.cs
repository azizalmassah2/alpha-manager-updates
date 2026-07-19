using System;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// وضع تشغيل التطبيق بعد فحص الترخيص
/// </summary>
public enum ApplicationMode
{
    /// <summary>وضع مجاني — بدون ترخيص</summary>
    Free,

    /// <summary>وضع احترافي — ترخيص صالح</summary>
    Pro
}

/// <summary>
/// حالة الترخيص المفصلة
/// </summary>
public enum LicenseState
{
    /// <summary>لا يوجد ملف ترخيص</summary>
    NoLicense,

    /// <summary>الترخيص صالح</summary>
    Valid,

    /// <summary>انتهت صلاحية الترخيص</summary>
    Expired,

    /// <summary>الترخيص لا يطابق الراوتر الحالي</summary>
    RouterMismatch,

    /// <summary>ملف الترخيص تالف</summary>
    Corrupted,

    /// <summary>التوقيع الرقمي غير صالح</summary>
    InvalidSignature
}

/// <summary>
/// كائن الجلسة المركزي — يُنشأ بعد الاتصال الناجح بالراوتر وفحص الترخيص.
/// يُمرَّر للـ Dashboard والنوافذ الأخرى ويحدد سلوك التطبيق بالكامل.
/// </summary>
public class ApplicationSession
{
    // ── معرف الجلسة ─────────────────────────────────────────────
    public Guid SessionId { get; } = Guid.NewGuid();
    public DateTime CreatedAt { get; } = DateTime.Now;

    // ── معلومات الراوتر ──────────────────────────────────────────
    /// <summary>معلومات الراوتر المتصل — null في حالة العمل بدون اتصال</summary>
    public RouterInfo? Router { get; set; }

    /// <summary>هل التطبيق متصل براوتر؟</summary>
    public bool IsConnected => Router != null;

    // ── حالة الترخيص ─────────────────────────────────────────────
    public bool IsLicensed { get; set; } = false;
    public LicenseState LicenseState { get; set; } = LicenseState.NoLicense;
    public string LicenseMessage { get; set; } = string.Empty;
    public DateTime? LicenseExpiresAt { get; set; }

    // ── وضع التطبيق ──────────────────────────────────────────────
    public ApplicationMode Mode { get; set; } = ApplicationMode.Free;

    // ── بيانات المستخدم ──────────────────────────────────────────
    public string UserName { get; set; } = "المستخدم";

    // ── رمز التحقق الأمني من الجلسة ──────────────────────────────
    public string SessionToken { get; set; } = string.Empty;

    // ── مساعدات ──────────────────────────────────────────────────
    public bool IsProMode => Mode == ApplicationMode.Pro && IsLicensed;
    public bool IsFreeMode => Mode == ApplicationMode.Free || !IsLicensed;

    /// <summary>إبطال الجلسة وتخفيضها للوضع المجاني فوراً لأسباب أمنية</summary>
    public void Invalidate()
    {
        IsLicensed = false;
        Mode = ApplicationMode.Free;
        SessionToken = string.Empty;
        LicenseMessage = "تم إبطال الجلسة أمنياً";
    }

    // ── مصانع ثابتة ──────────────────────────────────────────────

    /// <summary>إنشاء جلسة مجانية (لا ترخيص أو لا اتصال)</summary>
    public static ApplicationSession CreateFreeSession(RouterInfo? router = null)
    {
        return new ApplicationSession
        {
            Router = router,
            IsLicensed = false,
            LicenseState = LicenseState.NoLicense,
            Mode = ApplicationMode.Free,
            LicenseMessage = "وضع مجاني — لا يوجد ترخيص"
        };
    }

    /// <summary>إنشاء جلسة احترافية (ترخيص صالح)</summary>
    public static ApplicationSession CreateProSession(RouterInfo router, DateTime? expiresAt = null, string sessionToken = "")
    {
        return new ApplicationSession
        {
            Router = router,
            IsLicensed = true,
            LicenseState = LicenseState.Valid,
            Mode = ApplicationMode.Pro,
            LicenseExpiresAt = expiresAt,
            LicenseMessage = "ترخيص صالح",
            SessionToken = sessionToken
        };
    }
}
