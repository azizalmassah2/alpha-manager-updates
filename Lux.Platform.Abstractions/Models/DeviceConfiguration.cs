using System;
using System.Collections.Generic;

namespace Lux.Platform.Abstractions.Models;

public class DeviceConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Metadata { get; set; } = string.Empty;
    public ConfigurationMode Mode { get; set; } = ConfigurationMode.Merge;
    public List<ConfigurationSection> Sections { get; set; } = new();
}
