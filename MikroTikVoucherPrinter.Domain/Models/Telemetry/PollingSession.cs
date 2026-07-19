using System;

namespace MikroTikVoucherPrinter.Domain.Models.Telemetry;

public class PollingSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastPollAt { get; set; }
    
    public long PollCount { get; set; }
    public int ConsecutiveFailures { get; set; }
    
    public string CurrentStatus { get; set; } = "Initializing";
}
