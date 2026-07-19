namespace Lux.Management.Console.Core.Security.Models;

/// <summary>
/// سبب رفض طلب التفويض — يُعيد تفاصيل السبب دون الحاجة لإعادة تنفيذ المنطق.
/// </summary>
public enum AuthorizationFailureReason
{
    /// <summary>لا يوجد سبب (الطلب مقبول)</summary>
    None,
    
    /// <summary>المستخدم غير مُصادق عليه</summary>
    NotAuthenticated,
    
    /// <summary>الميزة غير متاحة في الترخيص الحالي</summary>
    FeatureNotLicensed,
    
    /// <summary>تم تجاوز الحد الأقصى المسموح للنسخة المجانية</summary>
    FreeTierLimitExceeded,
    
    /// <summary>رمز الجلسة غير صالح أو منتهي</summary>
    InvalidSession,
    
    /// <summary>الميزة معطلة بقرار الإدارة</summary>
    FeatureDisabled
}

/// <summary>
/// نتيجة طلب التفويض الممنهجة بقوة بدلاً من إرجاع bool مجرد.
/// تحتوي على سبب الرفض لتسجيله دون الحاجة لإعادة تنفيذ منطق الفحص.
/// </summary>
public sealed record AuthorizationResult(
    bool Allowed,
    FeatureId Feature,
    AuthorizationFailureReason? Reason = null
)
{
    public static AuthorizationResult Success(FeatureId feature) =>
        new(true, feature, null);

    public static AuthorizationResult Deny(FeatureId feature, AuthorizationFailureReason reason) =>
        new(false, feature, reason);
}
