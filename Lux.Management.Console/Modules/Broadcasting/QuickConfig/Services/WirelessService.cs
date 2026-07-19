using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services
{
    public class WirelessService : IWirelessService
    {
        private IUciService Uci => ServiceLocator.Instance.Resolve<IUciService>();
        private IUbusClient Ubus => ServiceLocator.Instance.Resolve<IUbusClient>();
        private ILoggerService Logger => ServiceLocator.Instance.Resolve<ILoggerService>();

        public async Task ConfigureRadioApAsync(string ip, string session, string radioName, string ifaceSection, string ssid, string password, string networkName)
        {
            Logger.Log($"جاري إعداد الواي فاي للراديو {radioName} (وضع AP) باسم الشبكة: {ssid} والشبكة {networkName}...");

            var section = ifaceSection;
            if (string.IsNullOrEmpty(section))
            {
                // Create a new wifi-iface if none existed
                Logger.Log($"لم يتم العثور على قسم wifi-iface للراديو {radioName}. جاري إنشاء قسم جديد...");
                section = await Uci.AddSectionAsync(ip, session, "wireless", "wifi-iface");
            }

            var values = new Dictionary<string, object>
            {
                { "device", radioName },
                { "mode", "ap" },
                { "ssid", ssid },
                { "network", networkName }
            };

            // Set encryption
            if (!string.IsNullOrWhiteSpace(password))
            {
                values["encryption"] = "psk2";
                values["key"] = password;
            }
            else
            {
                values["encryption"] = "none";
                // Delete key if it existed
                try { await Uci.DeleteAsync(ip, session, "wireless", section, "key"); } catch { }
            }

            // Always enable/enable wifi-device (disabled=0)
            try { await Uci.SetOptionAsync(ip, session, "wireless", radioName, "disabled", 0); } catch { }

            await Uci.SetAsync(ip, session, "wireless", section, values);
            Logger.LogSuccess($"تم إعداد الراديو {radioName} بنجاح.");
        }

        public async Task ConfigureRadioStaWdsAsync(string ip, string session, string radioName, string ifaceSection, string remoteSsid, string remotePassword, string networkName)
        {
            Logger.Log($"جاري إعداد الواي فاي للراديو {radioName} (وضع Client WDS) للاتصال بالشبكة البعيدة: {remoteSsid}...");

            var section = ifaceSection;
            if (string.IsNullOrEmpty(section))
            {
                Logger.Log($"لم يتم العثور على قسم wifi-iface للراديو {radioName}. جاري إنشاء قسم جديد...");
                section = await Uci.AddSectionAsync(ip, session, "wireless", "wifi-iface");
            }

            var values = new Dictionary<string, object>
            {
                { "device", radioName },
                { "mode", "sta" },
                { "wds", 1 },
                { "ssid", remoteSsid },
                { "network", networkName }
            };

            // Set encryption
            if (!string.IsNullOrWhiteSpace(remotePassword))
            {
                values["encryption"] = "psk2";
                values["key"] = remotePassword;
            }
            else
            {
                values["encryption"] = "none";
                try { await Uci.DeleteAsync(ip, session, "wireless", section, "key"); } catch { }
            }

            // Always enable wifi-device (disabled=0)
            try { await Uci.SetOptionAsync(ip, session, "wireless", radioName, "disabled", 0); } catch { }

            await Uci.SetAsync(ip, session, "wireless", section, values);
            Logger.LogSuccess($"تم إعداد الراديو {radioName} (عميل WDS) بنجاح.");
        }

        public async Task<List<ScanResult>> ScanNetworksAsync(string ip, string session, string radioName)
        {
            Logger.Log($"جاري فحص الشبكات اللاسلكية المحيطة باستخدام الراديو {radioName}...");
            var results = new List<ScanResult>();

            // In OpenWrt, physical wireless interface names are usually wlan0, wlan1, etc.
            // Let's try radio0 -> wlan0, radio1 -> wlan1, and also try radioName directly.
            var interfaceNamesToTry = new List<string> { radioName };
            if (radioName.EndsWith("0")) interfaceNamesToTry.Add("wlan0");
            if (radioName.EndsWith("1")) interfaceNamesToTry.Add("wlan1");
            
            // Also add generic wifi interface names
            interfaceNamesToTry.Add("wlan0");
            interfaceNamesToTry.Add("wlan1");

            Exception? lastEx = null;
            foreach (var iface in interfaceNamesToTry)
            {
                try
                {
                    var response = await Ubus.CallAsync(ip, session, "iwinfo", "scan", new { device = iface });
                    if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty("results", out var resultsProp) && resultsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in resultsProp.EnumerateArray())
                        {
                            var scanResult = ParseScanResult(item);
                            if (scanResult != null)
                            {
                                results.Add(scanResult);
                            }
                        }

                        if (results.Count > 0)
                        {
                            Logger.LogSuccess($"تم العثور على {results.Count} شبكة لاسلكية محيطة عبر الواجهة {iface}.");
                            return results;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }

            if (lastEx != null)
            {
                Logger.LogWarning($"لم نتمكن من الحصول على نتائج الفحص اللاسلكي. خطأ: {lastEx.Message}");
            }
            else
            {
                Logger.LogWarning("لم يعثر فحص الواي فاي على أي نتائج.");
            }

            return results;
        }

        private ScanResult? ParseScanResult(JsonElement item)
        {
            try
            {
                var ssid = item.TryGetProperty("ssid", out var sProp) ? sProp.GetString() : string.Empty;
                if (string.IsNullOrEmpty(ssid)) ssid = "<شبكة مخفية>";

                var signal = item.TryGetProperty("signal", out var sigProp) ? sigProp.GetInt32() : -100;
                var channel = item.TryGetProperty("channel", out var chProp) ? chProp.GetInt32() : 0;
                var frequency = item.TryGetProperty("frequency", out var freqProp) ? freqProp.GetDouble() : 0.0;
                var bssid = item.TryGetProperty("bssid", out var bProp) ? bProp.GetString() : string.Empty;

                // Format encryption
                var encryption = "مفتوح";
                if (item.TryGetProperty("encryption", out var encProp) && encProp.ValueKind == JsonValueKind.Object)
                {
                    if (encProp.TryGetProperty("enabled", out var enabledProp) && enabledProp.GetBoolean())
                    {
                        var wpa = false;
                        if (encProp.TryGetProperty("wpa", out var wpaProp) && wpaProp.ValueKind == JsonValueKind.Array)
                        {
                            wpa = wpaProp.GetArrayLength() > 0;
                        }

                        var wep = encProp.TryGetProperty("wep", out var wepProp) && wepProp.GetBoolean();

                        if (wpa)
                        {
                            // Try to get auth suites
                            var suite = "WPA2-PSK";
                            if (encProp.TryGetProperty("auth_suites", out var authProp) && authProp.ValueKind == JsonValueKind.Array && authProp.GetArrayLength() > 0)
                            {
                                var firstSuite = authProp[0].GetString() ?? "";
                                if (firstSuite.Contains("PSK")) suite = "WPA2-PSK";
                                else if (firstSuite.Contains("SAE")) suite = "WPA3-SAE";
                                else suite = firstSuite;
                            }
                            encryption = suite;
                        }
                        else if (wep)
                        {
                            encryption = "WEP";
                        }
                        else
                        {
                            encryption = "مشفّر";
                        }
                    }
                }

                // If frequency is in MHz (e.g. 5180), convert to GHz (5.18 GHz)
                if (frequency > 1000)
                {
                    frequency = Math.Round(frequency / 1000.0, 3);
                }

                return new ScanResult
                {
                    Ssid = ssid,
                    SignalStrength = signal,
                    Channel = channel,
                    Frequency = frequency,
                    EncryptionType = encryption,
                    Bssid = bssid ?? string.Empty
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
