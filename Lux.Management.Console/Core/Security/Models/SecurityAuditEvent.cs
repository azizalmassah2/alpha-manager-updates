using System;
using Lux.Management.Console.Core.Security.Audit;

namespace Lux.Management.Console.Core.Security.Models;

/// <summary>
/// الفئة الأساسية المشتركة لكافة أحداث التدقيق الأمني في النظام.
/// </summary>
public abstract class AuditEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public abstract AuditCategory Category { get; }
    public abstract AuditSeverity Severity { get; }
    public string Message { get; set; } = string.Empty;
    public string? ExceptionMessage { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public class AuthenticationAuditEvent : AuditEvent
{
    public override AuditCategory Category => AuditCategory.Authentication;
    public override AuditSeverity Severity { get; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Login, Logout, Failure

    public AuthenticationAuditEvent(AuditSeverity severity, string userName, string action, string message)
    {
        Severity = severity;
        UserName = userName;
        Action = action;
        Message = message;
    }
}

public class LicenseAuditEvent : AuditEvent
{
    public override AuditCategory Category => AuditCategory.Licensing;
    public override AuditSeverity Severity { get; }
    public string RouterId { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public LicenseAuditEvent(AuditSeverity severity, string routerId, string serialNumber, string status, string message)
    {
        Severity = severity;
        RouterId = routerId;
        SerialNumber = serialNumber;
        Status = status;
        Message = message;
    }
}

public class SessionAuditEvent : AuditEvent
{
    public override AuditCategory Category => AuditCategory.Session;
    public override AuditSeverity Severity { get; }
    public Guid ActiveSessionId { get; set; }
    public string Action { get; set; } = string.Empty; // Created, Invalidated, Expired

    public SessionAuditEvent(AuditSeverity severity, Guid sessionId, string action, string message)
    {
        Severity = severity;
        ActiveSessionId = sessionId;
        Action = action;
        Message = message;
    }
}

public class AuthorizationAuditEvent : AuditEvent
{
    public override AuditCategory Category => AuditCategory.Authorization;
    public override AuditSeverity Severity { get; }
    public FeatureId Feature { get; set; }
    public string UserName { get; set; } = string.Empty;

    public AuthorizationAuditEvent(AuditSeverity severity, FeatureId feature, string userName, string message)
    {
        Severity = severity;
        Feature = feature;
        UserName = userName;
        Message = message;
    }
}

public class TamperAuditEvent : AuditEvent
{
    public override AuditCategory Category => AuditCategory.Tamper;
    public override AuditSeverity Severity => AuditSeverity.Critical;
    public string Source { get; set; } = string.Empty; // Debugger, AssemblyIntegrity, DllInjection
    public string Details { get; set; } = string.Empty;

    public TamperAuditEvent(string source, string details, string message)
    {
        Source = source;
        Details = details;
        Message = message;
    }
}

public class MemoryAuditEvent : AuditEvent
{
    public override AuditCategory Category => AuditCategory.Memory;
    public override AuditSeverity Severity { get; }
    public string BufferType { get; set; } = string.Empty; // Key, Password, Payload

    public MemoryAuditEvent(AuditSeverity severity, string bufferType, string message)
    {
        Severity = severity;
        BufferType = bufferType;
        Message = message;
    }
}

public class RuntimeAuditEvent : AuditEvent
{
    public override AuditCategory Category => AuditCategory.Runtime;
    public override AuditSeverity Severity { get; }
    public string Action { get; set; } = string.Empty; // Started, Stopped, Failed

    public RuntimeAuditEvent(AuditSeverity severity, string action, string message)
    {
        Severity = severity;
        Action = action;
        Message = message;
    }
}

public class GenericSecurityAuditEvent : AuditEvent
{
    private readonly AuditCategory _category;
    private readonly AuditSeverity _severity;

    public override AuditCategory Category => _category;
    public override AuditSeverity Severity => _severity;

    public GenericSecurityAuditEvent(AuditCategory category, AuditSeverity severity, string message)
    {
        _category = category;
        _severity = severity;
        Message = message;
    }
}
