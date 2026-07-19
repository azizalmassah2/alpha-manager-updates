namespace Lux.Management.Console.Core.Security.Models;

/// <summary>
/// بصمة الهوية الرقمية الفريدة للراوتر المتصل.
/// </summary>
public sealed class RouterFingerprint
{
    public string SerialNumber { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
}
