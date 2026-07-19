using System;

namespace Lux.Platform.Abstractions.Models;

public class DeviceBackup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DeviceVendor Vendor { get; set; }
    public BackupType BackupType { get; set; } = BackupType.Configuration;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long SizeBytes { get; set; }
    public string Metadata { get; set; } = string.Empty;
}
