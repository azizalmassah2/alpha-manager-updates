using System.Collections.Generic;

namespace Lux.Platform.Abstractions.Models;

public class ConfigurationSection
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, string> Settings { get; set; } = new();
}
