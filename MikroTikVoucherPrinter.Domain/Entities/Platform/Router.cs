using System;
using MikroTikVoucherPrinter.Domain.Common;

namespace MikroTikVoucherPrinter.Domain.Entities.Platform;

public class Router : BaseEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 8728;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? EncryptedCredentialsReference { get; set; }
    
    // Telemetry and OS version info
    public string? RouterIdentity { get; set; }
    public string? RouterBoard { get; set; }
    public string? RouterOSVersion { get; set; }
    public string? SoftwareId { get; set; }
    public string? SerialNumber { get; set; }
    public string? MacAddress { get; set; }
    
    public DateTime? LastConnectedUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    
    public bool IsFavorite { get; set; }
    public string? Notes { get; set; }
}
