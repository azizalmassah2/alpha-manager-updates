using System;
using Lux.Management.Console.Core.Security.Models;

namespace Lux.Management.Console.Core.Security.Audit;

/// <summary>
/// فئات تصنيف الأحداث الأمنية في سجل التدقيق
/// </summary>
public enum AuditCategory
{
    Authentication,
    Licensing,
    Session,
    Authorization,
    Tamper,
    Memory,
    Runtime,
    Application
}

/// <summary>
/// مستويات خطورة الحدث الأمني
/// </summary>
public enum AuditSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// واجهة سجلات التدقيق الأمني المحلي (أوفلاين بالكامل - بدون تليمتري أو سحابة).
/// </summary>
public interface ISecurityAuditService
{
    /// <summary>تسجيل حدث أمني مشفر محلياً</summary>
    void LogEvent(AuditEvent auditEvent);

    /// <summary>تسجيل حدث أمني بطريقة مبسطة</summary>
    void LogEvent(AuditCategory category, AuditSeverity severity, string message, Exception? ex = null, string correlationId = "");
}
