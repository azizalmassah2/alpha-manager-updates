using System.Collections.Generic;

namespace Lux.MikroTik.Models;

public class MikroTikCommand
{
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string[]? Arguments { get; set; }
}
