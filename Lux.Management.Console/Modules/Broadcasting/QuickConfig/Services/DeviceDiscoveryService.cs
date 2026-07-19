using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services
{
    public class DeviceDiscoveryService : IDeviceDiscoveryService
    {
        private IUciService Uci => ServiceLocator.Instance.Resolve<IUciService>();
        private ILoggerService Logger => ServiceLocator.Instance.Resolve<ILoggerService>();

        public async Task<DeviceDiscoveryResult> DiscoverDeviceAsync(string ip, string session)
        {
            var result = new DeviceDiscoveryResult();
            Logger.Log($"جاري فحص واكتشاف إعدادات وأقسام الجهاز ديناميكياً ({ip})...");

            // 1. Discover Wireless Radios & Interfaces
            var wireless = await Uci.GetConfigDictAsync(ip, session, "wireless");
            var radio24 = string.Empty;
            var radio5 = string.Empty;

            foreach (var kvp in wireless)
            {
                var sectionName = kvp.Key;
                if (kvp.Value is Dictionary<string, object> sectionDict)
                {
                    if (sectionDict.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "wifi-device")
                    {
                        var is5Ghz = false;

                        // Check band
                        if (sectionDict.TryGetValue("band", out var bandVal))
                        {
                            var band = bandVal.ToString()?.ToLower();
                            if (band != null && (band.Contains("5g") || band.Contains("a")))
                            {
                                is5Ghz = true;
                            }
                        }
                        // Check hwmode
                        else if (sectionDict.TryGetValue("hwmode", out var hwmodeVal))
                        {
                            var hwmode = hwmodeVal.ToString()?.ToLower();
                            if (hwmode != null && (hwmode.Contains("11a") || hwmode.Contains("ac") || hwmode.Contains("ax") || hwmode.Contains("an")))
                            {
                                is5Ghz = true;
                            }
                        }
                        // Check channel
                        else if (sectionDict.TryGetValue("channel", out var channelVal) && double.TryParse(channelVal.ToString(), out var channel))
                        {
                            if (channel >= 36)
                            {
                                is5Ghz = true;
                            }
                        }

                        if (is5Ghz)
                        {
                            radio5 = sectionName;
                        }
                        else
                        {
                            radio24 = sectionName;
                        }
                    }
                }
            }

            // Fallback if not detected properly
            if (string.IsNullOrEmpty(radio24)) radio24 = "radio0";
            if (string.IsNullOrEmpty(radio5)) radio5 = "radio1";

            result.Radio24GhzName = radio24;
            result.Radio5GhzName = radio5;
            Logger.Log($"تم اكتشاف راديو 2.4GHz: {radio24}، وراديو 5GHz: {radio5}");

            // Find wifi-iface sections for each radio
            foreach (var kvp in wireless)
            {
                var sectionName = kvp.Key;
                if (kvp.Value is Dictionary<string, object> sectionDict)
                {
                    if (sectionDict.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "wifi-iface")
                    {
                        if (sectionDict.TryGetValue("device", out var deviceVal))
                        {
                            var device = deviceVal.ToString();
                            if (device == radio24 && string.IsNullOrEmpty(result.WifiIface24GhzSection))
                            {
                                result.WifiIface24GhzSection = sectionName;
                            }
                            else if (device == radio5 && string.IsNullOrEmpty(result.WifiIface5GhzSection))
                            {
                                result.WifiIface5GhzSection = sectionName;
                            }
                        }
                    }
                }
            }

            Logger.Log($"تم ربط واجهة 2.4GHz بالقسم: {result.WifiIface24GhzSection}، وواجهة 5GHz بالقسم: {result.WifiIface5GhzSection}");

            // 2. Discover Network Details & VLAN Architecture
            var network = await Uci.GetConfigDictAsync(ip, session, "network");
            var hasBridgeVlan = false;
            var hasSwitchVlan = false;
            var switchName = "switch0";

            foreach (var kvp in network)
            {
                var sectionName = kvp.Key;
                if (kvp.Value is Dictionary<string, object> sectionDict)
                {
                    if (sectionDict.TryGetValue(".type", out var typeVal))
                    {
                        var typeStr = typeVal.ToString();
                        if (typeStr == "bridge-vlan")
                        {
                            hasBridgeVlan = true;
                        }
                        else if (typeStr == "switch_vlan")
                        {
                            hasSwitchVlan = true;
                        }
                        else if (typeStr == "switch")
                        {
                            hasSwitchVlan = true;
                            switchName = sectionName;
                        }
                        else if (typeStr == "interface" && sectionName == "lan")
                        {
                            result.LanSectionName = sectionName;
                            if (sectionDict.TryGetValue("device", out var dev))
                            {
                                result.LanDeviceName = dev.ToString() ?? "br-lan";
                            }
                            else if (sectionDict.TryGetValue("ifname", out var ifname))
                            {
                                result.LanDeviceName = ifname.ToString() ?? "br-lan";
                            }
                        }
                    }
                }
            }

            if (hasBridgeVlan)
            {
                result.VlanType = VlanArchitecture.Dsa;
                Logger.Log("نوع هندسة VLAN المكتشفة: DSA (Bridge VLAN Filtering)");
            }
            else if (hasSwitchVlan)
            {
                result.VlanType = VlanArchitecture.SwConfig;
                result.SwitchName = switchName;
                
                // Let's guess CPU port for SwConfig based on typical hardware
                // CPU port is usually 6t or 5t or 0t. 6t is very common on MediaTek/Atheros OpenWrt routers.
                result.SwitchCpuPort = "6t"; 
                result.SwitchLanPorts = "1 2 3 4";
                
                Logger.Log($"نوع هندسة VLAN المكتشفة: SwConfig (المفتاح المكتشف: {switchName})");
            }
            else
            {
                result.VlanType = VlanArchitecture.Traditional;
                Logger.Log("نوع هندسة VLAN المكتشفة: Traditional (Bridge-VLAN Interface splitting)");
            }

            Logger.Log($"تم اكتشاف قسم LAN: {result.LanSectionName}، وجهاز LAN: {result.LanDeviceName}");
            return result;
        }
    }
}
