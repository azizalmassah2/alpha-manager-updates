namespace OpenWrtProgrammerPro.Models
{
    public class DeviceTemplate
    {
        public string TemplateName { get; set; } = string.Empty;
        public int VlanId { get; set; } = 1;
        public string Gateway { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = "255.255.255.0";
        public WirelessMode Mode { get; set; } = WirelessMode.AccessPoint;
        public string Ssid24Ghz { get; set; } = string.Empty;
        public string Ssid5Ghz { get; set; } = string.Empty;
    }
}
