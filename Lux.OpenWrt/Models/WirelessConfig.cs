namespace Lux.OpenWrt.Models;

public class WirelessConfig
{
    public WirelessMode Mode { get; set; } = WirelessMode.AccessPoint;
    public string Ssid24Ghz { get; set; } = string.Empty;
    public string Ssid5Ghz { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; } = false;
    public string WifiPassword { get; set; } = string.Empty;

    // For StationWds
    public string RemoteSsid { get; set; } = string.Empty;
    public string RemotePassword { get; set; } = string.Empty;
}
