using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Lux.Management.Console.Core.Security.Crypto;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Security.Configuration;

namespace Lux.Management.Console.Core.Security.Audit;

/// <summary>
/// تطبيق ISecurityAuditService لتسجيل الأحداث الأمنية محلياً بشكل مشفر بـ DPAPI ومعبر بصيغة JSON.
/// </summary>
public class SecurityAuditService : ISecurityAuditService
{
    private readonly IMemoryProtectionService _memoryProtectionService;
    private readonly object _lock = new();

    public SecurityAuditService(IMemoryProtectionService memoryProtectionService)
    {
        _memoryProtectionService = memoryProtectionService;
    }

    public void LogEvent(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        WriteAuditLog(auditEvent);
    }

    public void LogEvent(AuditCategory category, AuditSeverity severity, string message, Exception? ex = null, string correlationId = "")
    {
        var evt = new GenericSecurityAuditEvent(category, severity, message)
        {
            ExceptionMessage = ex?.ToString(),
            CorrelationId = correlationId
        };
        WriteAuditLog(evt);
    }

    private void WriteAuditLog(AuditEvent evt)
    {
        lock (_lock)
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LuxCard", "Audit");
                Directory.CreateDirectory(folder);

                var logPath = Path.Combine(folder, "security_audit.dat");
                
                // تحويل الكائن إلى سلسلة JSON للتدقيق بالاعتماد على النوع الفعلي للمشتق لضمان حفظ كل الخصائص
                var rawJson = JsonSerializer.Serialize(evt, evt.GetType());

                // تشفير السطر أمنياً بمستوى المستخدم الحالي لنظام التشغيل
                var encryptedLine = _memoryProtectionService.ProtectString(rawJson, SecurityConfiguration.AuditEntropy);

                if (!string.IsNullOrEmpty(encryptedLine))
                {
                    File.AppendAllLines(logPath, new[] { encryptedLine }, Encoding.UTF8);
                }
            }
            catch
            {
                // إخفاق صامت لتجنب إيقاف التطبيق بسبب أخطاء تعذر الكتابة في السجلات
            }
        }
    }
}
