using System;

namespace MikroTikVoucherPrinter.Domain.Entities.Platform
{
    public class VlanMonitoringConfig
    {
        public Guid RouterId { get; set; }
        public string VlanId { get; set; } = string.Empty;
        public string DeviceIp { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool EnableMonitoring { get; set; } = true;
    }
}
