using System;

namespace Lux.Platform.Abstractions.Models.Monitoring;

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
}
