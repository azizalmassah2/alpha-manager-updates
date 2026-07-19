namespace Lux.Management.Console.Core.Security.Policies;

/// <summary>
/// سياسة التدقيق الأمني وإعدادات التشفير وحفظ الأحداث.
/// </summary>
public sealed record AuditPolicy(
    int MaxEventStorageDays = 30,
    bool EncryptLogs = true,
    bool CompressOldLogs = true
);
