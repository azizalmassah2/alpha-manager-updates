using System;
using MikroTikVoucherPrinter.Domain.Enums.Platform;

namespace MikroTikVoucherPrinter.Domain.Models.Platform;

public class DiscoveredDevice
{
    public string Identity { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    
    public string Platform { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
    public string RouterOsVersion { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    
    public string SoftwareId { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    
    public string DiscoverySource { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    public DeviceClassification Classification { get; set; } = DeviceClassification.Unknown;
}
