using System;
using Lux.Platform.Abstractions;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Domain.Entities;

/// <summary>
/// ظٹظ…ط«ظ„ ط¬ظ‡ط§ط² ط´ط¨ظƒط© ظ…ط³ط¬ظ„ ظپظٹ ط§ظ„ظ…ظ†طµط© ط§ظ„ظ…ظˆط­ط¯ط© (MikroTik, OpenWrt, UBNT, ط§ظ„ط®)
/// </summary>
public class NetworkDevice : BaseEntity, IDevice
{
    public string Name { get; set; } = string.Empty;
    public DeviceVendor Vendor { get; set; }
    public string Model { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    
    // Explicitly implement IDevice.Id to satisfy string return type
    string IDevice.Id => this.Id.ToString();
    public string MacAddress { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public DeviceStatus Status { get; set; }
    public DateTime? LastSeen { get; set; }

    // ط¨ظٹط§ظ†ط§طھ ط§طھطµط§ظ„ ط§ط®طھظٹط§ط±ظٹط©/ظ…ط´ظپط±ط© (ظٹط¬ط¨ ط£ظ„ط§ طھط¹ط§ط¯ ظƒط¬ط²ط، ظ…ظ† ط§ظ„ظ€ DTO ط§ظ„ط§ظپطھط±ط§ط¶ظٹ)
    public string? Username { get; set; }
    public string? Password { get; set; } 
    
    // ط¨ظٹط§ظ†ط§طھ ط¥ط¶ط§ظپظٹط© (JSON)
    public string? Metadata { get; set; }
}
