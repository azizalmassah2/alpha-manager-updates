using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.Platform.Abstractions;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class DeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly IUciService _uci;
    private readonly IUbusClient _ubus;
    private readonly ILogger<DeviceDiscoveryService> _logger;

    public DeviceDiscoveryService(IUciService uci, IUbusClient ubus, ILogger<DeviceDiscoveryService> logger)
    {
        _uci = uci;
        _ubus = ubus;
        _logger = logger;
    }

    public async Task<Result<NetworkDevice>> DiscoverDeviceAsync(string ip, string session, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("ط¬ط§ط±ظٹ ظپط­طµ ظˆط§ظƒطھط´ط§ظپ ط¥ط¹ط¯ط§ط¯ط§طھ ظˆط£ظ‚ط³ط§ظ… ط§ظ„ط¬ظ‡ط§ط² ط¯ظٹظ†ط§ظ…ظٹظƒظٹط§ظ‹ ({Ip})...", ip);

            var device = new NetworkDevice
            {
                Id = Guid.NewGuid(),
                IpAddress = ip,
                Vendor = DeviceVendor.OpenWrt,
                Status = DeviceStatus.Online,
                LastSeen = DateTime.UtcNow,
                Name = "OpenWrt Router",
                Model = "Unknown OpenWrt Device",
                FirmwareVersion = "Unknown",
                MacAddress = string.Empty
            };

            // ظ…ط­ط§ظˆظ„ط© ط¬ظ„ط¨ ظ…ط¹ظ„ظˆظ…ط§طھ ط§ظ„ظ†ط¸ط§ظ… (System Board) ط¥ظ† ط£ظ…ظƒظ†
            try
            {
                var sysBoard = await _ubus.CallAsync(ip, session, "system", "board", null, cancellationToken);
                
                if (sysBoard.TryGetProperty("hostname", out var hostnameProp) && hostnameProp.ValueKind == JsonValueKind.String)
                {
                    device.Name = hostnameProp.GetString() ?? device.Name;
                }

                if (sysBoard.TryGetProperty("model", out var modelProp) && modelProp.ValueKind == JsonValueKind.String)
                {
                    device.Model = modelProp.GetString() ?? device.Model;
                }

                if (sysBoard.TryGetProperty("release", out var releaseProp) && releaseProp.ValueKind == JsonValueKind.Object)
                {
                    if (releaseProp.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == JsonValueKind.String)
                    {
                        device.FirmwareVersion = versionProp.GetString() ?? device.FirmwareVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "طھط¹ط°ط± ط§ط³طھط±ط¬ط§ط¹ ظ…ط¹ظ„ظˆظ…ط§طھ ط§ظ„ظ†ط¸ط§ظ… (system board) ظ„ظ„ط¬ظ‡ط§ط² {Ip}", ip);
            }

            // 1. Discover Wireless Radios & Interfaces
            var wireless = await _uci.GetConfigDictAsync(ip, session, "wireless", cancellationToken);
            var radio24 = string.Empty;
            var radio5 = string.Empty;
            var wifiIface24 = string.Empty;
            var wifiIface5 = string.Empty;

            foreach (var kvp in wireless)
            {
                var sectionName = kvp.Key;
                if (kvp.Value is Dictionary<string, object> sectionDict)
                {
                    if (sectionDict.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "wifi-device")
                    {
                        var is5Ghz = false;

                        if (sectionDict.TryGetValue("band", out var bandVal))
                        {
                            var band = bandVal.ToString()?.ToLower();
                            if (band != null && (band.Contains("5g") || band.Contains("a")))
                            {
                                is5Ghz = true;
                            }
                        }
                        else if (sectionDict.TryGetValue("hwmode", out var hwmodeVal))
                        {
                            var hwmode = hwmodeVal.ToString()?.ToLower();
                            if (hwmode != null && (hwmode.Contains("11a") || hwmode.Contains("ac") || hwmode.Contains("ax") || hwmode.Contains("an")))
                            {
                                is5Ghz = true;
                            }
                        }
                        else if (sectionDict.TryGetValue("channel", out var channelVal) && double.TryParse(channelVal.ToString(), out var channel))
                        {
                            if (channel >= 36)
                            {
                                is5Ghz = true;
                            }
                        }

                        if (is5Ghz) radio5 = sectionName;
                        else radio24 = sectionName;
                    }
                }
            }

            if (string.IsNullOrEmpty(radio24)) radio24 = "radio0";
            if (string.IsNullOrEmpty(radio5)) radio5 = "radio1";

            _logger.LogInformation("طھظ… ط§ظƒطھط´ط§ظپ ط±ط§ط¯ظٹظˆ 2.4GHz: {Radio24}طŒ ظˆط±ط§ط¯ظٹظˆ 5GHz: {Radio5}", radio24, radio5);

            foreach (var kvp in wireless)
            {
                var sectionName = kvp.Key;
                if (kvp.Value is Dictionary<string, object> sectionDict)
                {
                    if (sectionDict.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "wifi-iface")
                    {
                        if (sectionDict.TryGetValue("device", out var deviceVal))
                        {
                            var deviceName = deviceVal.ToString();
                            if (deviceName == radio24 && string.IsNullOrEmpty(wifiIface24))
                            {
                                wifiIface24 = sectionName;
                            }
                            else if (deviceName == radio5 && string.IsNullOrEmpty(wifiIface5))
                            {
                                wifiIface5 = sectionName;
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("طھظ… ط±ط¨ط· ظˆط§ط¬ظ‡ط© 2.4GHz ط¨ط§ظ„ظ‚ط³ظ…: {Wifi24}طŒ ظˆظˆط§ط¬ظ‡ط© 5GHz ط¨ط§ظ„ظ‚ط³ظ…: {Wifi5}", wifiIface24, wifiIface5);

            // 2. Discover Network Details & VLAN Architecture
            var network = await _uci.GetConfigDictAsync(ip, session, "network", cancellationToken);
            var hasBridgeVlan = false;
            var hasSwitchVlan = false;
            var switchName = "switch0";
            var lanSectionName = string.Empty;
            var lanDeviceName = string.Empty;

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
                            lanSectionName = sectionName;
                            if (sectionDict.TryGetValue("device", out var dev))
                            {
                                lanDeviceName = dev.ToString() ?? "br-lan";
                            }
                            else if (sectionDict.TryGetValue("ifname", out var ifname))
                            {
                                lanDeviceName = ifname.ToString() ?? "br-lan";
                            }
                        }
                    }
                }
            }

            var vlanTypeStr = "Traditional";
            var switchCpuPort = string.Empty;
            var switchLanPorts = string.Empty;

            if (hasBridgeVlan)
            {
                vlanTypeStr = "Dsa";
                _logger.LogInformation("ظ†ظˆط¹ ظ‡ظ†ط¯ط³ط© VLAN ط§ظ„ظ…ظƒطھط´ظپط©: DSA (Bridge VLAN Filtering)");
            }
            else if (hasSwitchVlan)
            {
                vlanTypeStr = "SwConfig";
                switchCpuPort = "6t"; 
                switchLanPorts = "1 2 3 4";
                _logger.LogInformation("ظ†ظˆط¹ ظ‡ظ†ط¯ط³ط© VLAN ط§ظ„ظ…ظƒطھط´ظپط©: SwConfig (ط§ظ„ظ…ظپطھط§ط­ ط§ظ„ظ…ظƒطھط´ظپ: {Switch})", switchName);
            }
            else
            {
                vlanTypeStr = "Traditional";
                _logger.LogInformation("ظ†ظˆط¹ ظ‡ظ†ط¯ط³ط© VLAN ط§ظ„ظ…ظƒطھط´ظپط©: Traditional (Bridge-VLAN Interface splitting)");
            }

            _logger.LogInformation("طھظ… ط§ظƒطھط´ط§ظپ ظ‚ط³ظ… LAN: {LanSection}طŒ ظˆط¬ظ‡ط§ط² LAN: {LanDevice}", lanSectionName, lanDeviceName);

            // ط­ظپط¸ ط¨ظٹط§ظ†ط§طھ ط§ظ„ط§ظƒطھط´ط§ظپ ط§ظ„ظ…ط®طµطµط© ظپظٹ Metadata ط§ظ„ط®ط§طµ ط¨ظ€ NetworkDevice
            var metadataObj = new
            {
                Radio24GhzName = radio24,
                Radio5GhzName = radio5,
                WifiIface24GhzSection = wifiIface24,
                WifiIface5GhzSection = wifiIface5,
                LanSectionName = lanSectionName,
                LanDeviceName = lanDeviceName,
                VlanType = vlanTypeStr,
                SwitchName = switchName,
                SwitchCpuPort = switchCpuPort,
                SwitchLanPorts = switchLanPorts
            };

            device.Metadata = JsonSerializer.Serialize(metadataObj);

            return Result<NetworkDevice>.Success(device);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("طھظ… ط¥ظ„ط؛ط§ط، ط¹ظ…ظ„ظٹط© ط§ظƒطھط´ط§ظپ ط§ظ„ط¬ظ‡ط§ط² {Ip} (Timeout/Cancel).", ip);
            return Result<NetworkDevice>.Failure($"ط§ظ†طھظ‡طھ ظ…ظ‡ظ„ط© ط§ط³طھظƒط´ط§ظپ ط§ظ„ط¬ظ‡ط§ط² ط£ظˆ طھظ… ط§ظ„ط¥ظ„ط؛ط§ط، ({ip})", ErrorType.ExternalService, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ط­ط¯ط« ط®ط·ط£ ط؛ظٹط± ظ…طھظˆظ‚ط¹ ط£ط«ظ†ط§ط، ط§ظƒطھط´ط§ظپ ط§ظ„ط¬ظ‡ط§ط² {Ip}: {Message}", ip, ex.Message);
            return Result<NetworkDevice>.Failure($"ظپط´ظ„ ظپظٹ ط§ظƒطھط´ط§ظپ ط¥ط¹ط¯ط§ط¯ط§طھ ط§ظ„ط¬ظ‡ط§ط²: {ex.Message}", ErrorType.Unexpected, ex);
        }
    }
}
