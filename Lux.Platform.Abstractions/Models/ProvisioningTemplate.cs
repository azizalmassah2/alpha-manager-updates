using System;

namespace Lux.Platform.Abstractions.Models;

public class ProvisioningTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The base configuration containing placeholders like {{IpAddress}}
    /// </summary>
    public DeviceConfiguration BaseConfiguration { get; set; } = new();
}
