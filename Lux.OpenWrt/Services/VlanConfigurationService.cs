using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class VlanConfigurationService : IVlanConfigurationService
{
    private readonly IUciService _uci;
    private readonly ILogger<VlanConfigurationService> _logger;

    public VlanConfigurationService(IUciService uci, ILogger<VlanConfigurationService> logger)
    {
        _uci = uci;
        _logger = logger;
    }

    public async Task CreateVlanAsync(string ip, string session, string lanDevice, string vlanTypeStr, int vlanId, string switchName, string switchCpuPort, string switchLanPorts, CancellationToken cancellationToken = default)
    {
        var vlanSectionName = $"vlan{vlanId}";
        _logger.LogInformation("جاري إنشاء شبكة VLAN معرف {VlanId} بنوع {VlanTypeStr}...", vlanId, vlanTypeStr);

        if (vlanTypeStr == "Dsa")
        {
            var bridgeVlanSection = $"br_lan_{vlanId}";
            var ports = new List<string>();
            var lanPortsArray = switchLanPorts.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in lanPortsArray) ports.Add($"{p}:t");
            
            ports.Add($"{switchCpuPort}"); 
            if (!switchCpuPort.Contains(':')) ports.Add($"{switchCpuPort}:t");
            
            var bridgeVlanValues = new Dictionary<string, object>
            {
                { "device", lanDevice },
                { "vlan", vlanId },
                { "ports", string.Join(" ", ports) }
            };

            try
            {
                await _uci.SetAsync(ip, session, "network", bridgeVlanSection, bridgeVlanValues, cancellationToken);
            }
            catch
            {
                await _uci.AddSectionAsync(ip, session, "network", "bridge-vlan", bridgeVlanSection, cancellationToken);
                await _uci.SetAsync(ip, session, "network", bridgeVlanSection, bridgeVlanValues, cancellationToken);
            }

            var ifaceValues = new Dictionary<string, object>
            {
                { "proto", "none" },
                { "device", $"{lanDevice}.{vlanId}" }
            };

            try
            {
                await _uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues, cancellationToken);
            }
            catch
            {
                await _uci.AddSectionAsync(ip, session, "network", "interface", vlanSectionName, cancellationToken);
                await _uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues, cancellationToken);
            }
        }
        else if (vlanTypeStr == "SwConfig")
        {
            var switchVlanSection = $"{switchName}_vlan_{vlanId}";
            var portsList = new List<string>();
            var lanPortsArray = switchLanPorts.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in lanPortsArray) portsList.Add($"{p}t");
            portsList.Add(switchCpuPort.EndsWith("t") ? switchCpuPort : $"{switchCpuPort}t");

            var switchVlanValues = new Dictionary<string, object>
            {
                { "device", switchName },
                { "vlan", vlanId },
                { "ports", string.Join(" ", portsList) }
            };

            try
            {
                await _uci.SetAsync(ip, session, "network", switchVlanSection, switchVlanValues, cancellationToken);
            }
            catch
            {
                await _uci.AddSectionAsync(ip, session, "network", "switch_vlan", switchVlanSection, cancellationToken);
                await _uci.SetAsync(ip, session, "network", switchVlanSection, switchVlanValues, cancellationToken);
            }

            var switchInterface = "eth0"; 
            var ifaceValues = new Dictionary<string, object>
            {
                { "proto", "none" },
                { "device", $"{switchInterface}.{vlanId}" }
            };

            try
            {
                await _uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues, cancellationToken);
            }
            catch
            {
                await _uci.AddSectionAsync(ip, session, "network", "interface", vlanSectionName, cancellationToken);
                await _uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues, cancellationToken);
            }
        }
        else
        {
            string vlanName = $"vlan{vlanId}";
            string vlanPort = $"br-lan.{vlanId}";
            var networkConfig = await _uci.GetConfigDictAsync(ip, session, "network", cancellationToken);

            string dev8021qSection = $"dev_vlan{vlanId}";
            string devBridgeSection = $"dev_vlan{vlanId}_bridge";
            vlanSectionName = $"vlan{vlanId}";

            foreach (var key in networkConfig.Keys)
            {
                if (networkConfig[key] is Dictionary<string, object> sec &&
                    sec.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "device" &&
                    sec.TryGetValue("type", out var devType) && devType.ToString() == "8021q" &&
                    sec.TryGetValue("name", out var devName) && devName.ToString() == vlanPort)
                {
                    dev8021qSection = key; break;
                }
            }

            foreach (var key in networkConfig.Keys)
            {
                if (networkConfig[key] is Dictionary<string, object> sec &&
                    sec.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "device" &&
                    sec.TryGetValue("type", out var devType) && devType.ToString() == "bridge" &&
                    sec.TryGetValue("name", out var devName) && devName.ToString() == vlanName)
                {
                    devBridgeSection = key; break;
                }
            }

            var dev8021qValues = new Dictionary<string, object>
            {
                { "type", "8021q" },
                { "name", vlanPort },
                { "ifname", "br-lan" },
                { "vid", vlanId },
                { "ipv6", "0" }
            };

            try { await _uci.SetAsync(ip, session, "network", dev8021qSection, dev8021qValues, cancellationToken); }
            catch
            {
                await _uci.AddSectionAsync(ip, session, "network", "device", dev8021qSection, cancellationToken);
                await _uci.SetAsync(ip, session, "network", dev8021qSection, dev8021qValues, cancellationToken);
            }

            var devBridgeValues = new Dictionary<string, object>
            {
                { "type", "bridge" },
                { "name", vlanName },
                { "ports", new List<string> { vlanPort } },
                { "ipv6", "0" }
            };

            try { await _uci.SetAsync(ip, session, "network", devBridgeSection, devBridgeValues, cancellationToken); }
            catch
            {
                await _uci.AddSectionAsync(ip, session, "network", "device", devBridgeSection, cancellationToken);
                await _uci.SetAsync(ip, session, "network", devBridgeSection, devBridgeValues, cancellationToken);
            }

            var ifaceValues = new Dictionary<string, object>
            {
                { "proto", "none" },
                { "device", vlanName }
            };

            try { await _uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues, cancellationToken); }
            catch
            {
                await _uci.AddSectionAsync(ip, session, "network", "interface", vlanSectionName, cancellationToken);
                await _uci.SetAsync(ip, session, "network", vlanSectionName, ifaceValues, cancellationToken);
            }
        }

        _logger.LogInformation("تم إنشاء VLAN {VlanId} بنجاح.", vlanId);
    }
}
