namespace Lux.Management.Console.Core.Security.Policies;

/// <summary>
/// سياسة المراقبة الدورية والإغلاق الطارئ عند اكتشاف التلاعب.
/// </summary>
public sealed record RuntimePolicy(
    int MonitoringIntervalSeconds = 30,
    int IntegrityRetryCount = 3,
    int MaximumRandomDelayMs = 500,
    bool EnableDebuggerDetection = true,
    bool EnableIntegrityChecks = true
);
