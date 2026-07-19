using System;

namespace Lux.Management.Console.Core.Security.Models;

public enum SessionState
{
    Active,
    Inactive
}

public enum LicenseState
{
    NoLicense,
    Valid,
    InvalidRouter,
    Corrupted,
    Expired
}

public enum RouterState
{
    Connected,
    Disconnected
}

public enum RuntimeState
{
    Monitoring,
    Stopped,
    Faulted
}

public enum IntegrityState
{
    Valid,
    Tampered
}

/// <summary>
/// لقطة تشخيصية كاملة لحالة الأمان الحالية للبرنامج بالذاكرة.
/// </summary>
public sealed class SecurityHealthSnapshot
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public SessionState Session { get; set; }
    public LicenseState License { get; set; }
    public RouterState Router { get; set; }
    public RuntimeState Runtime { get; set; }
    public IntegrityState Integrity { get; set; }
    public DateTime LastValidation { get; set; }
    public bool IsHealthy { get; set; }
}
