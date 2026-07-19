using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services
{
    public class NetworkService : INetworkService
    {
        private IUciService Uci => ServiceLocator.Instance.Resolve<IUciService>();
        private ILoggerService Logger => ServiceLocator.Instance.Resolve<ILoggerService>();

        public async Task SetLanIpAsync(string ip, string session, string lanSection, string ipaddr, string gateway, string netmask)
        {
            Logger.Log($"جاري ضبط إعدادات IP للقسم {lanSection}: العنوان={ipaddr}، البوابة={gateway}، القناع={netmask}...");
            
            var values = new Dictionary<string, object>
            {
                { "ipaddr", ipaddr },
                { "netmask", netmask }
            };

            if (!string.IsNullOrWhiteSpace(gateway))
            {
                values["gateway"] = gateway;
            }
            else
            {
                // Delete gateway if left empty
                try
                {
                    await Uci.DeleteAsync(ip, session, "network", lanSection, "gateway");
                }
                catch { /* Ignore if it didn't exist */ }
            }

            await Uci.SetAsync(ip, session, "network", lanSection, values);
            Logger.LogSuccess($"تم ضبط إعدادات IP لقسم {lanSection} بنجاح.");
        }

        public async Task CreateVlanAsync(string ip, string session, string lanDevice, VlanArchitecture vlanType, int vlanId, string switchName, string switchCpuPort, string switchLanPorts)
        {
            var vlanSectionName = $"vlan{vlanId}";
            Logger.Log($"جاري إنشاء شبكة VLAN معرف {vlanId} بنوع {vlanType}...");

            if (vlanType == VlanArchitecture.Dsa)
            {
                // DSA - Bridge VLAN Filtering Strategy
                // 1. Create a bridge-vlan section on bridge device (typically br-lan)
                var bridgeVlanSection = $"br_lan_{vlanId}";
                Logger.Log($"إنشاء قسم bridge-vlan باسم {bridgeVlanSection} على الجسر {lanDevice}...");
                
                // Formulate tagged ports list, e.g., "lan1:t lan2:t lan3:t lan4:t CPU:t"
                // Usually CPU is named 'CPU' or 'cpu'. Let's tag CPU and LAN ports.
                var ports = new List<string>();
                var lanPortsArray = switchLanPorts.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in lanPortsArray)
                {
                    ports.Add($"{p}:t");
                }
                ports.Add($"{switchCpuPort}"); // cpu port is already tagged e.g. "6t" or "5t", if just "cpu" add ":t"
                if (!switchCpuPort.Contains(':'))
                {
                    ports.Add($"{switchCpuPort}:t");
                }
                
                var bridgeVlanValues = new Dictionary<string, object>
                {
                    { "device", lanDevice },
                    { "vlan", vlanId },
                    { "ports", string.Join(" ", ports) }
                };

                try
                {
                    await Uci.SetAsync(ip, session, "network", bridgeVlanSection, bridgeVlanValues);
                }
                catch
                {
                    // If named set fails, add section and set
                    await Uci.AddSectionAsync(ip, session, "network", "bridge-vlan", bridgeVlanSection);
                    await Uci.SetAsync(ip, session, "network", bridgeVlanSection, bridgeVlanValues);
                }

                // 2. Create the interface section
                Logger.Log($"إنشاء واجهة شبكة للـ VLAN باسم {vlanSectionName}...");
                var ifaceValues = new Dictionary<string, object>
                {
                    { "proto", "none" },
                    { "device", $"{lanDevice}.{vlanId}" }
                };

                try
                {
                    await Uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues);
                }
                catch
                {
                    await Uci.AddSectionAsync(ip, session, "network", "interface", vlanSectionName);
                    await Uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues);
                }
            }
            else if (vlanType == VlanArchitecture.SwConfig)
            {
                // SwConfig Switch Strategy
                var switchVlanSection = $"{switchName}_vlan_{vlanId}";
                Logger.Log($"إنشاء قسم switch_vlan باسم {switchVlanSection} على المفتاح {switchName}...");

                // Ports list e.g. "1t 2t 3t 4t 6t"
                var portsList = new List<string>();
                var lanPortsArray = switchLanPorts.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in lanPortsArray)
                {
                    portsList.Add($"{p}t");
                }
                portsList.Add(switchCpuPort.EndsWith("t") ? switchCpuPort : $"{switchCpuPort}t");

                var switchVlanValues = new Dictionary<string, object>
                {
                    { "device", switchName },
                    { "vlan", vlanId },
                    { "ports", string.Join(" ", portsList) }
                };

                try
                {
                    await Uci.SetAsync(ip, session, "network", switchVlanSection, switchVlanValues);
                }
                catch
                {
                    await Uci.AddSectionAsync(ip, session, "network", "switch_vlan", switchVlanSection);
                    await Uci.SetAsync(ip, session, "network", switchVlanSection, switchVlanValues);
                }

                // Create the interface section linked to the switch interface (typically eth0.XX)
                Logger.Log($"إنشاء واجهة شبكة للـ VLAN باسم {vlanSectionName}...");
                
                // Usually eth0 is the base interface connected to switch
                var switchInterface = "eth0"; 
                var ifaceValues = new Dictionary<string, object>
                {
                    { "proto", "none" },
                    { "device", $"{switchInterface}.{vlanId}" } // OpenWrt 21+ uses device
                };

                try
                {
                    await Uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues);
                }
                catch
                {
                    await Uci.AddSectionAsync(ip, session, "network", "interface", vlanSectionName);
                    await Uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues);
                }
            }
            else
            {
                // Traditional Linux VLAN sub-interfaces on bridge
                string vlanName = $"vlan{vlanId}";
                string vlanPort = $"br-lan.{vlanId}";

                // 1. Detect existing VLAN config to decide whether to update or create
                var networkConfig = await Uci.GetConfigDictAsync(ip, session, "network");

                string dev8021qSection = $"dev_vlan{vlanId}";
                string devBridgeSection = $"dev_vlan{vlanId}_bridge";
                vlanSectionName = $"vlan{vlanId}";

                bool exists = false;

                // Search for existing 8021q device
                foreach (var key in networkConfig.Keys)
                {
                    if (networkConfig[key] is Dictionary<string, object> sec &&
                        sec.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "device" &&
                        sec.TryGetValue("type", out var devType) && devType.ToString() == "8021q" &&
                        sec.TryGetValue("name", out var devName) && devName.ToString() == vlanPort)
                    {
                        dev8021qSection = key;
                        exists = true;
                        break;
                    }
                }

                // Search for existing bridge device
                foreach (var key in networkConfig.Keys)
                {
                    if (networkConfig[key] is Dictionary<string, object> sec &&
                        sec.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "device" &&
                        sec.TryGetValue("type", out var devType) && devType.ToString() == "bridge" &&
                        sec.TryGetValue("name", out var devName) && devName.ToString() == vlanName)
                    {
                        devBridgeSection = key;
                        exists = true;
                        break;
                    }
                }

                // Search for existing interface
                if (networkConfig.TryGetValue(vlanSectionName, out var ifaceVal) && ifaceVal is Dictionary<string, object> ifaceSec &&
                    ifaceSec.TryGetValue(".type", out var ifaceType) && ifaceType.ToString() == "interface")
                {
                    exists = true;
                }

                // Print logging info
                if (exists)
                {
                    Logger.Log("[VLAN] Existing VLAN detected - updating.");
                }
                else
                {
                    Logger.Log("[VLAN] VLAN does not exist - creating.");
                }

                // Print debug info before sending any UBUS requests
                Logger.Log($"VLAN ID:\n{vlanId}");
                Logger.Log($"Bridge Name:\n{vlanName}");
                Logger.Log($"Port Name:\n{vlanPort}");
                Logger.Log($"Interface Name:\n{vlanSectionName}");
                Logger.Log($"Interface Device Reference:\n{vlanName}");

                // 2. Create/update 8021q device
                var dev8021qValues = new Dictionary<string, object>
                {
                    { "type", "8021q" },
                    { "name", vlanPort },
                    { "ifname", "br-lan" },
                    { "vid", vlanId },
                    { "ipv6", "0" }
                };

                try
                {
                    await Uci.SetAsync(ip, session, "network", dev8021qSection, dev8021qValues);
                }
                catch
                {
                    await Uci.AddSectionAsync(ip, session, "network", "device", dev8021qSection);
                    await Uci.SetAsync(ip, session, "network", dev8021qSection, dev8021qValues);
                }

                // 3. Create/update bridge device
                var devBridgeValues = new Dictionary<string, object>
                {
                    { "type", "bridge" },
                    { "name", vlanName },
                    { "ports", new List<string> { vlanPort } },
                    { "ipv6", "0" }
                };

                try
                {
                    await Uci.SetAsync(ip, session, "network", devBridgeSection, devBridgeValues);
                }
                catch
                {
                    await Uci.AddSectionAsync(ip, session, "network", "device", devBridgeSection);
                    await Uci.SetAsync(ip, session, "network", devBridgeSection, devBridgeValues);
                }

                // 4. Create/update interface
                var ifaceValues = new Dictionary<string, object>
                {
                    { "proto", "none" },
                    { "device", vlanName }
                };

                try
                {
                    await Uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues);
                }
                catch
                {
                    await Uci.AddSectionAsync(ip, session, "network", "interface", vlanSectionName);
                    await Uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues);
                }
            }

            Logger.LogSuccess($"تم إنشاء VLAN {vlanId} بنجاح.");
        }

        public async Task DisableDhcpAsync(string ip, string session, string lanSection)
        {
            Logger.Log($"جاري تعطيل خادم DHCP وجهاز IPv6 RA لقسم LAN ({lanSection})...");

            try
            {
                // Set dhcp.lan.ignore = 1
                await Uci.SetOptionAsync(ip, session, "dhcp", lanSection, "ignore", 1);

                // Delete ra, ra_flags, dhcpv6 properties
                try { await Uci.DeleteAsync(ip, session, "dhcp", lanSection, "ra"); } catch { }
                try { await Uci.DeleteAsync(ip, session, "dhcp", lanSection, "ra_flags"); } catch { }
                try { await Uci.DeleteAsync(ip, session, "dhcp", lanSection, "dhcpv6"); } catch { }

                Logger.LogSuccess("تم تعطيل خادم DHCP و IPv6 RA/dhcpv6 بنجاح.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"حدث خطأ أثناء تعطيل DHCP (قد لا يحتوي الجهاز على قسم DHCP): {ex.Message}");
            }
        }
    }
}
