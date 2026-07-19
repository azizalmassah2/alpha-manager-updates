namespace OpenWrtProgrammerPro.Models
{
    public class ProgrammingPreview
    {
        public string Hostnames { get; set; } = string.Empty;
        public string TargetIps { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string VlanId { get; set; } = string.Empty;
        public string Ssid24Ghz { get; set; } = string.Empty;
        public string Ssid5Ghz { get; set; } = string.Empty;
        public string ModeText { get; set; } = string.Empty;
        public string WifiPassword { get; set; } = string.Empty;
        
        // Client WDS fields
        public bool IsClientWds { get; set; }
        public string RemoteSsid { get; set; } = string.Empty;
        public string RemotePassword { get; set; } = string.Empty;
        
        public string DhcpStatusText => "معطل (تم إهمال DHCP وتعطيل IPv6 RA/dhcpv6)";
    }
}
