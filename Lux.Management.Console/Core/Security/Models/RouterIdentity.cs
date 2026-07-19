namespace Lux.Management.Console.Core.Security.Models;

/// <summary>
/// هوية الراوتر الكاملة — تتضمن معرفات متعددة لتمييز الراوتر ودعم التطور المستقبلي.
/// يُفضل على RouterFingerprint لأن الهوية قد تحتوي مستقبلاً بيانات إضافية (Board، Architecture، SoftwareId).
/// </summary>
public sealed record RouterIdentity(
    string SerialNumber,
    string HardwareId,
    string? Board = null,
    string? Architecture = null,
    string? SoftwareId = null
);
