namespace Lux.Management.Console.Core.Security.Policies;

/// <summary>
/// سياسة أمان دورة حياة الجلسة ومهلات الصلاحية.
/// </summary>
public sealed record SessionPolicy(
    int DefaultTimeoutHours = 24
);
