using System;

namespace MikroTikVoucherPrinter.Domain.Models.Platform;

public class DiscoverySession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    public int DevicesFound { get; set; }
    public int NewDevices { get; set; }
    public int ExistingDevices { get; set; }
    
    public System.Collections.Generic.List<DiscoveredDevice> DiscoveredDevices { get; set; } = new();
    
    public void Complete()
    {
        CompletedAt = DateTime.UtcNow;
    }
}
