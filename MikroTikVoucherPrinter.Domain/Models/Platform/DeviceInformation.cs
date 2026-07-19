using System;

namespace MikroTikVoucherPrinter.Domain.Models.Platform;

public class DeviceInformation
{
    public string Identity { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string SoftwareId { get; set; } = string.Empty;
    public string RouterOsVersion { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public TimeSpan Uptime { get; set; }
    public string Model { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
}
