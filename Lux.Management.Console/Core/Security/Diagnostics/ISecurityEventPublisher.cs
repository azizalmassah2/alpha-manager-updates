using Lux.Management.Console.Core.Security.Models;

namespace Lux.Management.Console.Core.Security.Diagnostics;

/// <summary>
/// واجهة ناشر أحداث الأمان — يفك الارتباط بين RuntimeMonitor وSecurityAuditService،
/// ويفتح الباب لمستهلكين متعددين (شاشة تشخيص، عدادات أداء، إلخ) دون تعديل المراقب.
/// </summary>
public interface ISecurityEventPublisher
{
    /// <summary>
    /// نشر حدث أمان لجميع المشتركين المسجلين.
    /// </summary>
    void Publish(AuditEvent securityEvent);
}
