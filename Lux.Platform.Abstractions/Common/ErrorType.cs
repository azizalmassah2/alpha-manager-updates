namespace Lux.Platform.Abstractions.Common;

/// <summary>
/// أنواع الأخطاء في النظام لتسهيل معالجتها في واجهة المستخدم
/// </summary>
public enum ErrorType
{
    /// <summary>بدون خطأ</summary>
    None = 0,
    
    /// <summary>خطأ في التحقق من صحة البيانات المُدخلة</summary>
    Validation = 1,
    
    /// <summary>العنصر غير موجود</summary>
    NotFound = 2,
    
    /// <summary>تعارض في البيانات (مثل وجود اسم مكرر)</summary>
    Conflict = 3,
    
    /// <summary>خطأ في المصادقة أو الصلاحيات</summary>
    Unauthorized = 4,
    
    /// <summary>خطأ من خدمة خارجية (مثل انقطاع الاتصال بالمايكروتك)</summary>
    ExternalService = 5,
    
    /// <summary>خطأ غير متوقع في النظام</summary>
    Unexpected = 6
}
