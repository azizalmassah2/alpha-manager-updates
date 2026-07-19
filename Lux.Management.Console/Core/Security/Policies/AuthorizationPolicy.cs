namespace Lux.Management.Console.Core.Security.Policies;

/// <summary>
/// سياسة التفويض وحدود الميزات للنسخة المجانية والمدفوعة.
/// </summary>
public sealed record AuthorizationPolicy(
    int MaxFreeVouchersLimit = 10,
    int MaxFreeProfilesLimit = 3,
    bool AllowFreeExport = false,
    bool AllowFreePrinting = true
);
