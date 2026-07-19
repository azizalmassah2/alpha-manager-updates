using System;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// إدارة الجلسة المركزية — Singleton يُخزن الـ ApplicationSession الحالي
/// ويُبلغ عن التغييرات
/// </summary>
public interface ISessionManager
{
    /// <summary>الجلسة النشطة الحالية — null قبل اكتمال Login</summary>
    ApplicationSession? CurrentSession { get; }

    /// <summary>هل الجلسة نشطة؟</summary>
    bool HasSession { get; }

    /// <summary>تعيين جلسة جديدة (يُستدعى من LoginViewModel بعد نجاح الاتصال)</summary>
    void SetSession(ApplicationSession session);

    /// <summary>مسح الجلسة الحالية (Logout)</summary>
    void ClearSession();

    /// <summary>حدث عند تغيير الجلسة</summary>
    event EventHandler<ApplicationSession?> SessionChanged;
}
