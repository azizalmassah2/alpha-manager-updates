using System.Collections.Generic;

namespace OpenWrtProgrammerPro.Models
{
    public enum VlanArchitecture
    {
        Traditional,
        Dsa,
        SwConfig
    }

    public class DeviceDiscoveryResult
    {
        // Wireless radios found
        public string Radio24GhzName { get; set; } = "radio0";
        public string Radio5GhzName { get; set; } = "radio1";

        // Existing interface section names for matching wifi-ifaces
        public string WifiIface24GhzSection { get; set; } = string.Empty;
        public string WifiIface5GhzSection { get; set; } = string.Empty;

        // LAN section details
        public string LanSectionName { get; set; } = "lan";
        public string LanDeviceName { get; set; } = "br-lan";

        // VLAN detection
        public VlanArchitecture VlanType { get; set; } = VlanArchitecture.Traditional;
        public string SwitchName { get; set; } = "switch0"; // For swconfig
        public string SwitchCpuPort { get; set; } = "6t"; // CPU port for swconfig (often 6t, 5t, or 0t)
        public string SwitchLanPorts { get; set; } = "1 2 3 4"; // Lan ports (often 1 2 3 4)
    }
}
