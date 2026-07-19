using System;

namespace MikroTikVoucherPrinter.Infrastructure.Monitoring
{
    public class VlanHealthChangedEvent
    {
        public Guid RouterId { get; set; }
        public string VlanId { get; set; } = string.Empty;
        public string DeviceIp { get; set; } = string.Empty;
        public string Status { get; set; } = "Offline"; // "Healthy", "Warning", "Offline"
        public double LatencyMs { get; set; }
        public DateTime? LastSeen { get; set; }
    }
}
