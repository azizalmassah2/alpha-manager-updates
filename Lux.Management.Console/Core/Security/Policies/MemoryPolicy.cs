namespace Lux.Management.Console.Core.Security.Policies;

/// <summary>
/// سياسة الحماية من الاستنزاف الذاكري ومعالجة الكائنات الحساسة.
/// </summary>
public sealed record MemoryPolicy(
    bool ZeroSensitiveBuffersOnDispose = true,
    bool UseSecureStringForPasswords = true,
    int GcCollectAfterSessionEnd = 2
);
