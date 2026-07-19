using System;

namespace Lux.MikroTik.Models;

public class MikroTikDeviceInfo
{
    public string Identity { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public TimeSpan Uptime { get; set; }
}
