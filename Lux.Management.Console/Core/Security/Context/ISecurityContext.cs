using System;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Session;

namespace Lux.Management.Console.Core.Security.Context;

/// <summary>
/// واجهة سياق الأمان المركزي (للقراءة فقط) - المصدر الوحيد للحقيقة لبيانات الترخيص والجلسة والراوتر المتصل.
/// </summary>
public interface ISecurityContext
{
    /// <summary>هل يوجد مستخدم مسجل دخوله حالياً ومتصل براوتر؟</summary>
    bool IsAuthenticated { get; }

    /// <summary>الجلسة النشطة الحالية</summary>
    ApplicationSession? CurrentSession { get; }

    /// <summary>هل التطبيق يعمل في الوضع الاحترافي (Premium/Pro)؟</summary>
    bool IsProMode { get; }

    /// <summary>معلومات الراوتر النشط المتصل بالجلسة</summary>
    RouterInfo? CurrentRouter { get; }

    /// <summary>مجموعة الميزات المتاحة حالياً للترخيص الفعال (لقطة ثابتة غير قابلة للتعديل)</summary>
    FeatureSet CurrentFeatureSet { get; }

    /// <summary>توقيت تسجيل الدخول والبدء للجلسة</summary>
    DateTime? LoginTimestamp { get; }

    /// <summary>المعرف الفريد للجلسة الحالية</summary>
    Guid SessionId { get; }

    /// <summary>رمز التحقق الأمني الموقع للجلسة</summary>
    string SessionToken { get; }

    /// <summary>حدث ينطلق عند تعديل أو تحديث سياق الأمان</summary>
    event EventHandler? ContextChanged;
}
