using System;

namespace Lux.Platform.Abstractions.Models.Monitoring;

public class AlertRule
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public Func<DeviceState, bool> Condition { get; set; } = _ => false;
}
