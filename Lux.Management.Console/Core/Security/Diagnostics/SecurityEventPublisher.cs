using Lux.Management.Console.Core.Security.Audit;
using Lux.Management.Console.Core.Security.Models;

namespace Lux.Management.Console.Core.Security.Diagnostics;

/// <summary>
/// تطبيق ISecurityEventPublisher — يستقبل أحداث الأمان وينشرها لكافة المشتركين المسجلين.
/// يفك الارتباط بين RuntimeMonitor والتدقيق ويفتح الباب لمستهلكين متعددين دون تعديل المراقب.
/// </summary>
public sealed class SecurityEventPublisher : ISecurityEventPublisher
{
    private readonly ISecurityAuditService _auditService;

    public SecurityEventPublisher(ISecurityAuditService auditService)
    {
        _auditService = auditService;
    }

    public void Publish(AuditEvent securityEvent)
    {
        // المشترك الافتراضي: خدمة التدقيق الأمني
        _auditService.LogEvent(securityEvent);

        // يمكن مستقبلاً إضافة مشتركين آخرين هنا:
        // _performanceMonitor.Record(securityEvent);
        // _diagnosticsScreen.Update(securityEvent);
    }
}
