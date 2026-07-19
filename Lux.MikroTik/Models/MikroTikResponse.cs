using System.Collections.Generic;

namespace Lux.MikroTik.Models;

public class MikroTikResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<Dictionary<string, string>> RawData { get; set; } = new List<Dictionary<string, string>>();
}
