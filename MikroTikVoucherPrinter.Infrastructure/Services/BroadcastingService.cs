using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// خدمة أجهزة البث — تقوم بمسح الشبكة المحلية عبر ARP/Ping وإدارة الأجهزة المسجلة
/// </summary>
public class BroadcastingService : IBroadcastingService
{
    private readonly PlatformDbContext _db;

    // ── MAC OUI → Vendor mapping (بادئات معروفة) ────────────────────────
    private static readonly Dictionary<string, string> _ouiVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Ubiquiti
        { "00:15:6D", "Ubiquiti" }, { "00:27:22", "Ubiquiti" }, { "04:18:D6", "Ubiquiti" },
        { "0C:80:63", "Ubiquiti" }, { "24:A4:3C", "Ubiquiti" }, { "44:D9:E7", "Ubiquiti" },
        { "68:72:51", "Ubiquiti" }, { "74:83:C2", "Ubiquiti" }, { "78:8A:20", "Ubiquiti" },
        { "80:2A:A8", "Ubiquiti" }, { "B4:FB:E4", "Ubiquiti" }, { "DC:9F:DB", "Ubiquiti" },
        { "F0:9F:C2", "Ubiquiti" }, { "FC:EC:DA", "Ubiquiti" },
        // TP-Link
        { "14:CC:20", "TP-Link" }, { "18:D6:C7", "TP-Link" }, { "1C:FA:68", "TP-Link" },
        { "2C:4D:54", "TP-Link" }, { "40:16:9F", "TP-Link" }, { "50:C7:BF", "TP-Link" },
        { "54:AF:97", "TP-Link" }, { "60:32:B1", "TP-Link" }, { "64:70:02", "TP-Link" },
        { "6C:5A:B0", "TP-Link" }, { "70:4F:57", "TP-Link" }, { "7C:8B:CA", "TP-Link" },
        { "84:16:F9", "TP-Link" }, { "90:F6:52", "TP-Link" }, { "A0:F3:C1", "TP-Link" },
        { "AC:84:C6", "TP-Link" }, { "B0:BE:76", "TP-Link" }, { "C4:E9:84", "TP-Link" },
        { "D8:0D:17", "TP-Link" }, { "E8:DE:27", "TP-Link" }, { "F4:EC:38", "TP-Link" },
        // MikroTik
        { "08:55:31", "MikroTik" }, { "18:FD:74", "MikroTik" }, { "2C:C8:1B", "MikroTik" },
        { "48:8F:5A", "MikroTik" }, { "4C:5E:0C", "MikroTik" }, { "6C:3B:6B", "MikroTik" },
        { "74:4D:28", "MikroTik" }, { "78:9A:18", "MikroTik" }, { "B8:69:F4", "MikroTik" },
        { "CC:2D:E0", "MikroTik" }, { "D4:CA:6D", "MikroTik" }, { "DC:2C:6E", "MikroTik" },
        { "E4:8D:8C", "MikroTik" },
        // Huawei
        { "00:18:82", "Huawei" }, { "00:1E:10", "Huawei" }, { "00:25:9E", "Huawei" },
        { "04:02:1F", "Huawei" }, { "10:47:80", "Huawei" }, { "20:F3:A3", "Huawei" },
        { "28:31:52", "Huawei" }, { "30:D1:7E", "Huawei" }, { "38:37:8B", "Huawei" },
        { "4C:54:99", "Huawei" }, { "5C:C3:07", "Huawei" }, { "68:A0:F6", "Huawei" },
        { "80:71:7A", "Huawei" }, { "9C:28:EF", "Huawei" }, { "AC:E2:15", "Huawei" },
        { "B4:15:13", "Huawei" }, { "C8:94:BB", "Huawei" }, { "E8:08:8B", "Huawei" },
        // Cambium
        { "00:04:56", "Cambium" }, { "58:C1:7A", "Cambium" }, { "74:9D:8F", "Cambium" },
        // NETGEAR
        { "00:14:6C", "NETGEAR" }, { "28:C6:8E", "NETGEAR" }, { "A0:21:B7", "NETGEAR" },
        // D-Link
        { "00:1B:11", "D-Link" }, { "14:D6:4D", "D-Link" }, { "1C:7E:E5", "D-Link" },
    };

    public BroadcastingService(PlatformDbContext db)
    {
        _db = db;
    }

    // ────────────────────────────────────────────────────────────────────
    // كشف الأجهزة المحلية — Smart Neighbor Discovery
    // ────────────────────────────────────────────────────────────────────

    public async Task<List<DiscoveredNetworkDevice>> ScanLocalNetworkAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var discoveredMap = new Dictionary<string, DiscoveredNetworkDevice>(StringComparer.OrdinalIgnoreCase);

        var subnets = GetActiveSubnets();
        if (subnets.Count == 0)
        {
            progress?.Report("لم يتم العثور على شبكة محلية نشطة.");
            return new List<DiscoveredNetworkDevice>();
        }

        progress?.Report("جاري الاستماع للبروتوكولات (LLDP / CDP / MNDP)...");
        var discoveryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        var discoveryTask = ListenForDiscoveryProtocolsAsync(discoveredMap, 4000, discoveryCts.Token);

        try
        {
            await discoveryTask;
        }
        catch { }

        progress?.Report("جاري تصفية وإكمال معلومات الأجهزة المكتشفة...");
        var arpEntries = ReadArpTable();

        lock (discoveredMap)
        {
            foreach (var device in discoveredMap.Values.ToList())
            {
                if (!string.IsNullOrEmpty(device.MacAddress) && string.IsNullOrEmpty(device.IpAddress))
                {
                    var matchedIp = arpEntries.FirstOrDefault(x => x.Value.Equals(device.MacAddress, StringComparison.OrdinalIgnoreCase)).Key;
                    if (!string.IsNullOrEmpty(matchedIp))
                    {
                        device.IpAddress = matchedIp;
                    }
                }
                else if (!string.IsNullOrEmpty(device.IpAddress) && string.IsNullOrEmpty(device.MacAddress))
                {
                    if (arpEntries.TryGetValue(device.IpAddress, out var matchedMac))
                    {
                        device.MacAddress = matchedMac;
                    }
                }

                if (!string.IsNullOrEmpty(device.MacAddress) && (string.IsNullOrEmpty(device.Vendor) || device.Vendor == "غير معروف"))
                {
                    device.Vendor = ResolveVendor(device.MacAddress);
                }
            }
        }

        List<DiscoveredNetworkDevice> finalDevices;
        lock (discoveredMap)
        {
            finalDevices = discoveredMap.Values
                .Where(d => IsValidIpOrMac(d.IpAddress, d.MacAddress))
                .OrderBy(d => ParseIp(d.IpAddress))
                .ToList();
        }

        progress?.Report($"اكتمل فحص الجيران. تم اكتشاف {finalDevices.Count} أجهزة ذكية تعلن عن نفسها.");
        return finalDevices;
    }

    public async Task StartListeningAsync(
        Action<DiscoveredNetworkDevice> onDeviceUpdated,
        CancellationToken cancellationToken)
    {
        var discoveredMap = new Dictionary<string, DiscoveredNetworkDevice>(StringComparer.OrdinalIgnoreCase);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // تشغيل مستمعي المنافذ العامة
        var mndpListener = Task.Run(() => ListenMndpContinuousAsync(discoveredMap, onDeviceUpdated, cts.Token), cts.Token);
        var ubntListener = Task.Run(() => ListenUbntContinuousAsync(discoveredMap, onDeviceUpdated, cts.Token), cts.Token);
        var rawL2Listener = Task.Run(() => ListenRawL2ContinuousAsync(discoveredMap, onDeviceUpdated, cts.Token), cts.Token);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // إرسال طلبات نشطة واستقبال ردودها على منافذ عشوائية (لتخطي الجدار الناري)
                await SendDiscoveryQueriesAndListenAsync(discoveredMap, onDeviceUpdated, cancellationToken);

                // قراءة جدول ARP لتكملة البيانات
                var arpEntries = ReadArpTable();
                lock (discoveredMap)
                {
                    foreach (var device in discoveredMap.Values.ToList())
                    {
                        bool updated = false;
                        if (!string.IsNullOrEmpty(device.MacAddress) && string.IsNullOrEmpty(device.IpAddress))
                        {
                            var matchedIp = arpEntries.FirstOrDefault(x => x.Value.Equals(device.MacAddress, StringComparison.OrdinalIgnoreCase)).Key;
                            if (!string.IsNullOrEmpty(matchedIp))
                            {
                                device.IpAddress = matchedIp;
                                updated = true;
                            }
                        }
                        else if (!string.IsNullOrEmpty(device.IpAddress) && string.IsNullOrEmpty(device.MacAddress))
                        {
                            if (arpEntries.TryGetValue(device.IpAddress, out var matchedMac))
                            {
                                device.MacAddress = matchedMac;
                                updated = true;
                            }
                        }

                        if (!string.IsNullOrEmpty(device.MacAddress) && (string.IsNullOrEmpty(device.Vendor) || device.Vendor == "غير معروف"))
                        {
                            device.Vendor = ResolveVendor(device.MacAddress);
                            updated = true;
                        }

                        if (updated)
                        {
                            onDeviceUpdated?.Invoke(device);
                        }
                    }
                }

                // الانتظار 3.5 ثانية (الإجمالي 5 ثوانٍ مع الـ 1.5 ثانية للردود)
                await Task.Delay(3500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch { }
        }

        cts.Cancel();
        try
        {
            await Task.WhenAll(mndpListener, ubntListener, rawL2Listener);
        }
        catch { }
    }

    private List<string> GetActiveSubnets()
    {
        var subnets = new HashSet<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    var ip = addr.Address.ToString();
                    var parts = ip.Split('.');
                    if (parts.Length == 4)
                    {
                        subnets.Add($"{parts[0]}.{parts[1]}.{parts[2]}");
                    }
                }
            }
        }
        return subnets.ToList();
    }

    private static Dictionary<string, string> ReadArpTable()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("arp", "-a")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            var regex = new Regex(
                @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\s+([\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2}[-:][\da-fA-F]{2})",
                RegexOptions.Compiled);

            foreach (Match m in regex.Matches(output))
            {
                var ip  = m.Groups[1].Value;
                var mac = m.Groups[2].Value.Replace("-", ":").ToUpper();

                if (IsValidIpOrMac(ip, mac))
                {
                    if (!result.ContainsKey(ip))
                        result[ip] = mac;
                }
            }
        }
        catch { }
        return result;
    }

    private static bool IsValidIpOrMac(string ip, string mac)
    {
        if (string.IsNullOrEmpty(ip) && string.IsNullOrEmpty(mac)) return false;
        
        if (!string.IsNullOrEmpty(mac))
        {
            if (mac.Equals("FF:FF:FF:FF:FF:FF", StringComparison.OrdinalIgnoreCase)) return false;
            if (mac.StartsWith("01:00:5E", StringComparison.OrdinalIgnoreCase)) return false;
            if (mac.StartsWith("01:80:C2", StringComparison.OrdinalIgnoreCase)) return false;
            if (mac.StartsWith("33:33", StringComparison.OrdinalIgnoreCase)) return false;
        }
        
        if (!string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out var parsedIp))
        {
            var bytes = parsedIp.GetAddressBytes();
            if (bytes.Length == 4)
            {
                if (bytes[0] >= 224 && bytes[0] <= 239) return false;
                if (bytes[0] == 127) return false;
                if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255) return false;
            }
        }
        return true;
    }

    private static string ResolveVendor(string mac)
    {
        if (string.IsNullOrEmpty(mac) || mac.Length < 8) return "غير معروف";
        var oui = mac.Substring(0, 8).ToUpper();
        if (_ouiVendors.TryGetValue(oui, out var vendor))
        {
            return vendor;
        }

        try
        {
            var firstByteStr = mac.Substring(0, 2);
            byte firstByte = Convert.ToByte(firstByteStr, 16);
            if ((firstByte & 0x02) != 0) 
            {
                byte universalByte = (byte)(firstByte & ~0x02);
                var universalOui = universalByte.ToString("X2") + mac.Substring(2, 6);
                if (_ouiVendors.TryGetValue(universalOui, out vendor))
                {
                    return vendor;
                }
            }
        }
        catch { }

        return "غير معروف";
    }

    private static long ParseIp(string ip)
    {
        try
        {
            var parts = ip.Split('.');
            return long.Parse(parts[0]) * 16777216L
                 + long.Parse(parts[1]) * 65536L
                 + long.Parse(parts[2]) * 256L
                 + long.Parse(parts[3]);
        }
        catch { return 0; }
    }

    private static List<IPAddress> GetBroadcastAddresses()
    {
        var ips = new List<IPAddress>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var unicast in ni.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        var ipBytes = unicast.Address.GetAddressBytes();
                        var maskBytes = unicast.IPv4Mask?.GetAddressBytes();
                        if (maskBytes != null && maskBytes.Length == 4)
                        {
                            var broadcastBytes = new byte[4];
                            for (int i = 0; i < 4; i++)
                            {
                                broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                            }
                            ips.Add(new IPAddress(broadcastBytes));
                        }
                    }
                }
            }
        }
        catch { }

        if (ips.Count == 0)
        {
            ips.Add(IPAddress.Broadcast);
        }
        return ips;
    }

    // ── بروتوكولات كشف الجيران (LLDP / CDP / MNDP) ─────────────────────────

    private class MndpPacketInfo
    {
        public string MacAddress { get; set; } = "";
        public string Identity { get; set; } = "";
        public string Version { get; set; } = "";
        public string Platform { get; set; } = "";
        public string BoardName { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Interface { get; set; } = "";
        public string Uptime { get; set; } = "";
    }

    private class UbntPacketInfo
    {
        public string MacAddress { get; set; } = "";
        public string Identity { get; set; } = "";
        public string Version { get; set; } = "";
        public string BoardName { get; set; } = "";
        public string IpAddress { get; set; } = "";
    }

    private async Task ListenForDiscoveryProtocolsAsync(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        int durationMs,
        CancellationToken ct)
    {
        var mndpTask = ListenMndpAsync(discoveredMap, durationMs, ct);
        var ubntTask = SendAndListenUbntAsync(discoveredMap, durationMs, ct);
        var rawTask = ListenRawL2FramesAsync(discoveredMap, durationMs, ct);
        await Task.WhenAll(mndpTask, ubntTask, rawTask);
    }

    private async Task ListenMndpAsync(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        int durationMs,
        CancellationToken ct)
    {
        using var udp = new UdpClient();
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 5678));
            udp.EnableBroadcast = true;
        }
        catch
        {
            return;
        }

        try
        {
            byte[] query = new byte[] { 0x00, 0x01, 0x00, 0x01 };
            foreach (var bcast in GetBroadcastAddresses())
            {
                await udp.SendAsync(query, query.Length, new IPEndPoint(bcast, 5678));
            }
        }
        catch { }

        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalMilliseconds < durationMs && !ct.IsCancellationRequested)
        {
            if (udp.Available > 0)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var info = ParseMndp(result.Buffer);
                    if (info != null && !string.IsNullOrEmpty(info.MacAddress))
                    {
                        var mac = info.MacAddress.ToUpper();
                        lock (discoveredMap)
                        {
                            DiscoveredNetworkDevice? device = null;
                            var entryByMac = discoveredMap.Values.FirstOrDefault(d => d.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase));
                            if (entryByMac != null)
                            {
                                device = entryByMac;
                            }
                            else if (!string.IsNullOrEmpty(info.IpAddress) && discoveredMap.TryGetValue(info.IpAddress, out var entryByIp))
                            {
                                device = entryByIp;
                                device.MacAddress = mac;
                            }
                            else if (!string.IsNullOrEmpty(info.IpAddress))
                            {
                                device = new DiscoveredNetworkDevice
                                {
                                    IpAddress = info.IpAddress,
                                    MacAddress = mac,
                                    IsReachable = true
                                };
                                discoveredMap[info.IpAddress] = device;
                            }

                            if (device != null)
                            {
                                device.Hostname = info.Identity;
                                device.Vendor = "MikroTik";
                                device.Platform = "RouterOS";
                                device.Version = info.Version;
                                device.Protocol = "MNDP";
                                device.Interface = info.Interface;
                                device.Uptime = info.Uptime;

                                if (!string.IsNullOrEmpty(info.BoardName))
                                {
                                    device.Platform = $"RouterOS ({info.BoardName})";
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            else
            {
                await Task.Delay(50, ct);
            }
        }
    }

    private async Task SendAndListenUbntAsync(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        int durationMs,
        CancellationToken ct)
    {
        using var udp = new UdpClient();
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 10001));
            udp.EnableBroadcast = true;
        }
        catch
        {
            return;
        }

        try
        {
            byte[] payload = new byte[] { 0x01, 0x00, 0x00, 0x00 };
            foreach (var bcast in GetBroadcastAddresses())
            {
                await udp.SendAsync(payload, payload.Length, new IPEndPoint(bcast, 10001));
            }
        }
        catch { }

        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalMilliseconds < durationMs && !ct.IsCancellationRequested)
        {
            if (udp.Available > 0)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var info = ParseUbnt(result.Buffer);
                    if (info != null && !string.IsNullOrEmpty(info.MacAddress))
                    {
                        var mac = info.MacAddress.ToUpper();
                        lock (discoveredMap)
                        {
                            DiscoveredNetworkDevice? device = null;
                            var entryByMac = discoveredMap.Values.FirstOrDefault(d => d.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase));
                            if (entryByMac != null)
                            {
                                device = entryByMac;
                            }
                            else if (!string.IsNullOrEmpty(info.IpAddress) && discoveredMap.TryGetValue(info.IpAddress, out var entryByIp))
                            {
                                device = entryByIp;
                                device.MacAddress = mac;
                            }
                            else if (!string.IsNullOrEmpty(info.IpAddress))
                            {
                                device = new DiscoveredNetworkDevice
                                {
                                    IpAddress = info.IpAddress,
                                    MacAddress = mac,
                                    IsReachable = true
                                };
                                discoveredMap[info.IpAddress] = device;
                            }

                            if (device != null)
                            {
                                device.Hostname = info.Identity;
                                device.Vendor = "Ubiquiti";
                                device.Protocol = "UADP";
                                device.Version = info.Version;
                                device.Platform = info.BoardName;
                            }
                        }
                    }
                }
                catch { }
            }
            else
            {
                await Task.Delay(50, ct);
            }
        }
    }

    private async Task ListenRawL2FramesAsync(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        int durationMs,
        CancellationToken ct)
    {
        try
        {
            using var socket = new Socket((AddressFamily)18, SocketType.Raw, (ProtocolType)0x88CC);
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            
            byte[] buffer = new byte[2048];
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < durationMs && !ct.IsCancellationRequested)
            {
                if (socket.Available > 0)
                {
                    int bytesReceived = socket.Receive(buffer);
                    if (bytesReceived > 0)
                    {
                        byte[] frame = new byte[bytesReceived];
                        Array.Copy(buffer, frame, bytesReceived);
                        ParseL2Frame(frame, discoveredMap);
                    }
                }
                else
                {
                    await Task.Delay(50, ct);
                }
            }
        }
        catch (SocketException) { }
        catch (Exception) { }
    }

    private static void ParseL2Frame(byte[] frame, Dictionary<string, DiscoveredNetworkDevice> discoveredMap)
    {
        if (frame.Length < 14) return;
        ushort etherType = (ushort)((frame[12] << 8) | frame[13]);
        
        if (etherType == 0x8100 && frame.Length >= 18)
        {
            etherType = (ushort)((frame[16] << 8) | frame[17]);
        }

        if (etherType == 0x88CC)
        {
            ParseLldp(frame, discoveredMap);
        }
        else if (etherType == 0x2000 || (frame.Length > 20 && frame[12] == 0xaa && frame[13] == 0xaa))
        {
            ParseCdp(frame, discoveredMap);
        }
    }

    private static void ParseLldp(byte[] frame, Dictionary<string, DiscoveredNetworkDevice> discoveredMap)
    {
        if (frame.Length < 14) return;

        ushort etherType = (ushort)((frame[12] << 8) | frame[13]);
        int offset = 14;

        if (etherType == 0x8100 && frame.Length >= 18)
        {
            etherType = (ushort)((frame[16] << 8) | frame[17]);
            offset = 18;
        }

        if (etherType != 0x88CC) return;

        string srcMac = string.Join(":", frame.Skip(6).Take(6).Select(b => b.ToString("X2")));

        var device = new DiscoveredNetworkDevice
        {
            MacAddress = srcMac,
            Protocol = "LLDP",
            IsReachable = true
        };

        while (offset + 2 <= frame.Length)
        {
            ushort temp = (ushort)((frame[offset] << 8) | frame[offset + 1]);
            ushort type = (ushort)(temp >> 9);
            ushort length = (ushort)(temp & 0x01FF);
            offset += 2;

            if (type == 0) break;
            if (offset + length > frame.Length) break;

            byte[] value = new byte[length];
            Array.Copy(frame, offset, value, 0, length);
            offset += length;

            switch (type)
            {
                case 1:
                    if (length > 1)
                    {
                        device.MacAddress = string.Join(":", value.Skip(1).Select(b => b.ToString("X2")));
                    }
                    break;
                case 2:
                    if (length > 1)
                    {
                        byte subtype = value[0];
                        if (subtype == 5 || subtype == 7)
                        {
                            device.Interface = System.Text.Encoding.UTF8.GetString(value, 1, length - 1).Trim('\0');
                        }
                        else
                        {
                            device.Interface = $"Port {subtype}";
                        }
                    }
                    break;
                case 3:
                    if (length == 2)
                    {
                        ushort ttl = (ushort)((value[0] << 8) | value[1]);
                        device.Uptime = $"{ttl}s (TTL)";
                    }
                    break;
                case 5:
                    device.Hostname = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    break;
                case 6:
                    string desc = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    device.Platform = desc.Length > 30 ? desc.Substring(0, 30) + "..." : desc;
                    break;
                case 8:
                    if (length >= 5)
                    {
                        byte addrSubtype = value[1];
                        if (addrSubtype == 1) // IPv4
                        {
                            device.IpAddress = $"{value[2]}.{value[3]}.{value[4]}.{value[5]}";
                        }
                    }
                    break;
            }
        }

        if (!string.IsNullOrEmpty(device.MacAddress))
        {
            device.MacAddress = device.MacAddress.ToUpper();
            device.Vendor = ResolveVendor(device.MacAddress);
            lock (discoveredMap)
            {
                discoveredMap[device.MacAddress] = device;
            }
        }
    }

    private static void ParseCdp(byte[] frame, Dictionary<string, DiscoveredNetworkDevice> discoveredMap)
    {
        if (frame.Length < 22) return;

        string srcMac = string.Join(":", frame.Skip(6).Take(6).Select(b => b.ToString("X2")));

        int offset = 22;
        if (frame.Length < offset + 4) return;

        byte version = frame[offset];
        byte ttl = frame[offset + 1];
        offset += 4;

        var device = new DiscoveredNetworkDevice
        {
            MacAddress = srcMac,
            Protocol = "CDP",
            Uptime = $"{ttl}s (TTL)",
            IsReachable = true
        };

        while (offset + 4 <= frame.Length)
        {
            ushort type = (ushort)((frame[offset] << 8) | frame[offset + 1]);
            ushort length = (ushort)((frame[offset + 2] << 8) | frame[offset + 3]);
            offset += 4;

            if (length < 4 || offset + length - 4 > frame.Length) break;

            byte[] value = new byte[length - 4];
            Array.Copy(frame, offset, value, 0, length - 4);
            offset += length - 4;

            switch (type)
            {
                case 0x0001:
                    device.Hostname = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    break;
                case 0x0002:
                    if (value.Length >= 10)
                    {
                        int valOffset = 4;
                        if (valOffset + 6 <= value.Length)
                        {
                            byte protoType = value[valOffset];
                            byte protoLen = value[valOffset + 1];
                            if (protoLen == 1 && value[valOffset + 2] == 0xCC)
                            {
                                ushort addrLen = (ushort)((value[valOffset + 3] << 8) | value[valOffset + 4]);
                                if (valOffset + 5 + addrLen <= value.Length && addrLen == 4)
                                {
                                    device.IpAddress = $"{value[valOffset + 5]}.{value[valOffset + 6]}.{value[valOffset + 7]}.{value[valOffset + 8]}";
                                }
                            }
                        }
                    }
                    break;
                case 0x0003:
                    device.Interface = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    break;
                case 0x0005:
                    string ver = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    device.Version = ver.Length > 40 ? ver.Substring(0, 40) + "..." : ver;
                    break;
                case 0x0006:
                    device.Platform = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    break;
            }
        }

        if (!string.IsNullOrEmpty(device.MacAddress))
        {
            device.MacAddress = device.MacAddress.ToUpper();
            device.Vendor = ResolveVendor(device.MacAddress);
            lock (discoveredMap)
            {
                discoveredMap[device.MacAddress] = device;
            }
        }
    }

    private static MndpPacketInfo? ParseMndp(byte[] data)
    {
        if (data.Length < 4) return null;

        var info = new MndpPacketInfo();
        int offset = 0;

        // إذا كان يبتدئ برأس Winbox استكشافي 0x00 0x01 0x00 0x01 نتجاوزه
        if (data.Length >= 4 && data[0] == 0x00 && data[1] == 0x01 && data[2] == 0x00 && data[3] == 0x01)
        {
            offset = 4;
        }

        bool parsedAny = false;
        while (offset + 4 <= data.Length)
        {
            ushort type = (ushort)((data[offset] << 8) | data[offset + 1]);
            ushort length = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
            offset += 4;

            if (offset + length > data.Length) break;

            byte[] valBytes = new byte[length];
            Array.Copy(data, offset, valBytes, 0, length);
            offset += length;
            parsedAny = true;

            switch (type)
            {
                case 1:
                    if (length == 6)
                        info.MacAddress = string.Join(":", valBytes.Select(b => b.ToString("X2")));
                    break;
                case 2:
                    info.Identity = System.Text.Encoding.UTF8.GetString(valBytes).Trim('\0');
                    break;
                case 3:
                    info.Version = System.Text.Encoding.UTF8.GetString(valBytes).Trim('\0');
                    break;
                case 4:
                    info.Platform = System.Text.Encoding.UTF8.GetString(valBytes).Trim('\0');
                    break;
                case 5:
                    if (length == 4)
                    {
                        uint seconds = (uint)((valBytes[0] << 24) | (valBytes[1] << 16) | (valBytes[2] << 8) | valBytes[3]);
                        TimeSpan span = TimeSpan.FromSeconds(seconds);
                        info.Uptime = $"{span.Days}d {span.Hours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
                    }
                    break;
                case 8:
                    info.BoardName = System.Text.Encoding.UTF8.GetString(valBytes).Trim('\0');
                    break;
                case 10:
                    info.Interface = System.Text.Encoding.UTF8.GetString(valBytes).Trim('\0');
                    break;
                case 11:
                    if (length == 4)
                        info.IpAddress = $"{valBytes[0]}.{valBytes[1]}.{valBytes[2]}.{valBytes[3]}";
                    break;
            }
        }
        return parsedAny ? info : null;
    }

    private static UbntPacketInfo? ParseUbnt(byte[] data)
    {
        if (data.Length < 4) return null;
        byte version = data[0];
        byte cmd = data[1];
        ushort dataLen = (ushort)((data[2] << 8) | data[3]);
        if (data.Length < 4 + dataLen) return null;

        var info = new UbntPacketInfo();
        int offset = 4;
        int end = 4 + dataLen;

        while (offset + 3 <= end)
        {
            byte type = data[offset];
            ushort length = (ushort)((data[offset + 1] << 8) | data[offset + 2]);
            offset += 3;

            if (offset + length > end) break;

            byte[] valBytes = new byte[length];
            Array.Copy(data, offset, valBytes, 0, length);
            offset += length;

            switch (type)
            {
                case 1:
                    if (length == 6)
                        info.MacAddress = string.Join(":", valBytes.Select(b => b.ToString("X2")));
                    break;
                case 2:
                    if (length >= 10)
                    {
                        info.MacAddress = string.Join(":", valBytes.Take(6).Select(b => b.ToString("X2")));
                        info.IpAddress = $"{valBytes[6]}.{valBytes[7]}.{valBytes[8]}.{valBytes[9]}";
                    }
                    break;
                case 3:
                    info.Version = System.Text.Encoding.UTF8.GetString(valBytes).Trim('\0');
                    break;
                case 11:
                    info.Identity = System.Text.Encoding.UTF8.GetString(valBytes).Trim('\0');
                    break;
                case 12:
                    info.BoardName = System.Text.Encoding.UTF8.GetString(valBytes).Trim('\0');
                    break;
            }
        }
        return info;
    }

    // ────────────────────────────────────────────────────────────────────
    // دوال ومهام الاستماع المستمر والكشف التلقائي (Continuous Discovery)
    // ────────────────────────────────────────────────────────────────────

    private async Task ListenMndpContinuousAsync(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        Action<DiscoveredNetworkDevice> onDeviceUpdated,
        CancellationToken ct)
    {
        using var udp = new UdpClient();
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 5678));
            udp.EnableBroadcast = true;
        }
        catch
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            if (udp.Available > 0)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var info = ParseMndp(result.Buffer);
                    if (info != null)
                    {
                        MergeAndNotify(discoveredMap, info, "MNDP", onDeviceUpdated);
                    }
                }
                catch { }
            }
            else
            {
                await Task.Delay(100, ct);
            }
        }
    }

    private async Task ListenUbntContinuousAsync(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        Action<DiscoveredNetworkDevice> onDeviceUpdated,
        CancellationToken ct)
    {
        using var udp = new UdpClient();
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 10001));
            udp.EnableBroadcast = true;
        }
        catch
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            if (udp.Available > 0)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var info = ParseUbnt(result.Buffer);
                    if (info != null)
                    {
                        MergeAndNotifyUbnt(discoveredMap, info, onDeviceUpdated);
                    }
                }
                catch { }
            }
            else
            {
                await Task.Delay(100, ct);
            }
        }
    }

    private async Task ListenRawL2ContinuousAsync(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        Action<DiscoveredNetworkDevice> onDeviceUpdated,
        CancellationToken ct)
    {
        try
        {
            using var socket = new Socket((AddressFamily)18, SocketType.Raw, (ProtocolType)0x88CC);
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            
            byte[] buffer = new byte[2048];
            while (!ct.IsCancellationRequested)
            {
                if (socket.Available > 0)
                {
                    int bytesReceived = socket.Receive(buffer);
                    if (bytesReceived > 0)
                    {
                        byte[] frame = new byte[bytesReceived];
                        Array.Copy(buffer, frame, bytesReceived);
                        ParseL2FrameContinuous(frame, discoveredMap, onDeviceUpdated);
                    }
                }
                else
                {
                    await Task.Delay(100, ct);
                }
            }
        }
        catch (SocketException) { }
        catch (Exception) { }
    }

    private static void ParseL2FrameContinuous(
        byte[] frame,
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        Action<DiscoveredNetworkDevice> onDeviceUpdated)
    {
        if (frame.Length < 14) return;
        ushort etherType = (ushort)((frame[12] << 8) | frame[13]);
        
        if (etherType == 0x8100 && frame.Length >= 18)
        {
            etherType = (ushort)((frame[16] << 8) | frame[17]);
        }

        if (etherType == 0x88CC)
        {
            ParseLldpContinuous(frame, discoveredMap, onDeviceUpdated);
        }
        else if (etherType == 0x2000 || (frame.Length > 20 && frame[12] == 0xaa && frame[13] == 0xaa))
        {
            ParseCdpContinuous(frame, discoveredMap, onDeviceUpdated);
        }
    }

    private static void ParseLldpContinuous(
        byte[] frame,
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        Action<DiscoveredNetworkDevice> onDeviceUpdated)
    {
        if (frame.Length < 14) return;

        ushort etherType = (ushort)((frame[12] << 8) | frame[13]);
        int offset = 14;

        if (etherType == 0x8100 && frame.Length >= 18)
        {
            etherType = (ushort)((frame[16] << 8) | frame[17]);
            offset = 18;
        }

        if (etherType != 0x88CC) return;

        string srcMac = string.Join(":", frame.Skip(6).Take(6).Select(b => b.ToString("X2")));

        var device = new DiscoveredNetworkDevice
        {
            MacAddress = srcMac,
            Protocol = "LLDP",
            IsReachable = true
        };

        while (offset + 2 <= frame.Length)
        {
            ushort temp = (ushort)((frame[offset] << 8) | frame[offset + 1]);
            ushort type = (ushort)(temp >> 9);
            ushort length = (ushort)(temp & 0x01FF);
            offset += 2;

            if (type == 0) break;
            if (offset + length > frame.Length) break;

            byte[] value = new byte[length];
            Array.Copy(frame, offset, value, 0, length);
            offset += length;

            switch (type)
            {
                case 1:
                    if (length > 1)
                    {
                        device.MacAddress = string.Join(":", value.Skip(1).Select(b => b.ToString("X2")));
                    }
                    break;
                case 2:
                    if (length > 1)
                    {
                        byte subtype = value[0];
                        if (subtype == 5 || subtype == 7)
                        {
                            device.Interface = System.Text.Encoding.UTF8.GetString(value, 1, length - 1).Trim('\0');
                        }
                        else
                        {
                            device.Interface = $"Port {subtype}";
                        }
                    }
                    break;
                case 3:
                    if (length == 2)
                    {
                        ushort ttl = (ushort)((value[0] << 8) | value[1]);
                        device.Uptime = $"{ttl}s (TTL)";
                    }
                    break;
                case 5:
                    device.Hostname = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    break;
                case 6:
                    string desc = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    device.Platform = desc.Length > 30 ? desc.Substring(0, 30) + "..." : desc;
                    break;
                case 8:
                    if (length >= 5)
                    {
                        byte addrSubtype = value[1];
                        if (addrSubtype == 1) // IPv4
                        {
                            device.IpAddress = $"{value[2]}.{value[3]}.{value[4]}.{value[5]}";
                        }
                    }
                    break;
            }
        }

        if (!string.IsNullOrEmpty(device.MacAddress))
        {
            device.MacAddress = device.MacAddress.ToUpper();
            device.Vendor = ResolveVendor(device.MacAddress);
            lock (discoveredMap)
            {
                var entryByMac = discoveredMap.Values.FirstOrDefault(d => d.MacAddress.Equals(device.MacAddress, StringComparison.OrdinalIgnoreCase));
                if (entryByMac != null)
                {
                    entryByMac.Hostname = device.Hostname;
                    entryByMac.Vendor = device.Vendor;
                    entryByMac.Protocol = device.Protocol;
                    entryByMac.Interface = device.Interface;
                    entryByMac.Uptime = device.Uptime;
                    entryByMac.Platform = device.Platform;
                    if (!string.IsNullOrEmpty(device.IpAddress))
                    {
                        entryByMac.IpAddress = device.IpAddress;
                    }
                    device = entryByMac;
                }
                else
                {
                    if (!string.IsNullOrEmpty(device.IpAddress))
                    {
                        discoveredMap[device.IpAddress] = device;
                    }
                    else
                    {
                        discoveredMap[device.MacAddress] = device;
                    }
                }
            }
            onDeviceUpdated?.Invoke(device);
        }
    }

    private static void ParseCdpContinuous(
        byte[] frame,
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        Action<DiscoveredNetworkDevice> onDeviceUpdated)
    {
        if (frame.Length < 22) return;

        string srcMac = string.Join(":", frame.Skip(6).Take(6).Select(b => b.ToString("X2")));

        int offset = 22;
        if (frame.Length < offset + 4) return;

        byte version = frame[offset];
        byte ttl = frame[offset + 1];
        offset += 4;

        var device = new DiscoveredNetworkDevice
        {
            MacAddress = srcMac,
            Protocol = "CDP",
            Uptime = $"{ttl}s (TTL)",
            IsReachable = true
        };

        while (offset + 4 <= frame.Length)
        {
            ushort type = (ushort)((frame[offset] << 8) | frame[offset + 1]);
            ushort length = (ushort)((frame[offset + 2] << 8) | frame[offset + 3]);
            offset += 4;

            if (length < 4 || offset + length - 4 > frame.Length) break;

            byte[] value = new byte[length - 4];
            Array.Copy(frame, offset, value, 0, length - 4);
            offset += length - 4;

            switch (type)
            {
                case 0x0001:
                    device.Hostname = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    break;
                case 0x0002:
                    if (value.Length >= 10)
                    {
                        int valOffset = 4;
                        if (valOffset + 6 <= value.Length)
                        {
                            byte protoType = value[valOffset];
                            byte protoLen = value[valOffset + 1];
                            if (protoLen == 1 && value[valOffset + 2] == 0xCC)
                            {
                                ushort addrLen = (ushort)((value[valOffset + 3] << 8) | value[valOffset + 4]);
                                if (valOffset + 5 + addrLen <= value.Length && addrLen == 4)
                                {
                                    device.IpAddress = $"{value[valOffset + 5]}.{value[valOffset + 6]}.{value[valOffset + 7]}.{value[valOffset + 8]}";
                                }
                            }
                        }
                    }
                    break;
                case 0x0003:
                    device.Interface = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    break;
                case 0x0005:
                    string ver = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    device.Version = ver.Length > 40 ? ver.Substring(0, 40) + "..." : ver;
                    break;
                case 0x0006:
                    device.Platform = System.Text.Encoding.UTF8.GetString(value).Trim('\0');
                    break;
            }
        }

        if (!string.IsNullOrEmpty(device.MacAddress))
        {
            device.MacAddress = device.MacAddress.ToUpper();
            device.Vendor = ResolveVendor(device.MacAddress);
            lock (discoveredMap)
            {
                var entryByMac = discoveredMap.Values.FirstOrDefault(d => d.MacAddress.Equals(device.MacAddress, StringComparison.OrdinalIgnoreCase));
                if (entryByMac != null)
                {
                    entryByMac.Hostname = device.Hostname;
                    entryByMac.Vendor = device.Vendor;
                    entryByMac.Protocol = device.Protocol;
                    entryByMac.Interface = device.Interface;
                    entryByMac.Uptime = device.Uptime;
                    entryByMac.Platform = device.Platform;
                    if (!string.IsNullOrEmpty(device.IpAddress))
                    {
                        entryByMac.IpAddress = device.IpAddress;
                    }
                    device = entryByMac;
                }
                else
                {
                    if (!string.IsNullOrEmpty(device.IpAddress))
                    {
                        discoveredMap[device.IpAddress] = device;
                    }
                    else
                    {
                        discoveredMap[device.MacAddress] = device;
                    }
                }
            }
            onDeviceUpdated?.Invoke(device);
        }
    }

    private async Task SendDiscoveryQueriesAndListenAsync(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        Action<DiscoveredNetworkDevice> onDeviceUpdated,
        CancellationToken ct)
    {
        var bcasts = GetBroadcastAddresses();

        // 1. MNDP query
        var mndpTask = Task.Run(async () =>
        {
            try
            {
                using var client = new UdpClient();
                client.EnableBroadcast = true;
                byte[] query = new byte[] { 0x00, 0x01, 0x00, 0x01 };
                foreach (var bcast in bcasts)
                {
                    await client.SendAsync(query, query.Length, new IPEndPoint(bcast, 5678));
                }

                var start = DateTime.UtcNow;
                while ((DateTime.UtcNow - start).TotalMilliseconds < 1500 && !ct.IsCancellationRequested)
                {
                    if (client.Available > 0)
                    {
                        var result = await client.ReceiveAsync(ct);
                        var info = ParseMndp(result.Buffer);
                        if (info != null)
                        {
                            MergeAndNotify(discoveredMap, info, "MNDP", onDeviceUpdated);
                        }
                    }
                    else
                    {
                        await Task.Delay(30, ct);
                    }
                }
            }
            catch { }
        }, ct);

        // 2. Ubnt query
        var ubntTask = Task.Run(async () =>
        {
            try
            {
                using var client = new UdpClient();
                client.EnableBroadcast = true;
                byte[] query = new byte[] { 0x01, 0x00, 0x00, 0x00 };
                foreach (var bcast in bcasts)
                {
                    await client.SendAsync(query, query.Length, new IPEndPoint(bcast, 10001));
                }

                var start = DateTime.UtcNow;
                while ((DateTime.UtcNow - start).TotalMilliseconds < 1500 && !ct.IsCancellationRequested)
                {
                    if (client.Available > 0)
                    {
                        var result = await client.ReceiveAsync(ct);
                        var info = ParseUbnt(result.Buffer);
                        if (info != null)
                        {
                            MergeAndNotifyUbnt(discoveredMap, info, onDeviceUpdated);
                        }
                    }
                    else
                    {
                        await Task.Delay(30, ct);
                    }
                }
            }
            catch { }
        }, ct);

        await Task.WhenAll(mndpTask, ubntTask);
    }

    private static DiscoveredNetworkDevice MergeAndNotify(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        MndpPacketInfo info,
        string protocol,
        Action<DiscoveredNetworkDevice> onDeviceUpdated)
    {
        if (info == null || string.IsNullOrEmpty(info.MacAddress)) return null!;
        var mac = info.MacAddress.ToUpper();

        DiscoveredNetworkDevice? device = null;
        lock (discoveredMap)
        {
            var entryByMac = discoveredMap.Values.FirstOrDefault(d => d.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase));
            if (entryByMac != null)
            {
                device = entryByMac;
            }
            else if (!string.IsNullOrEmpty(info.IpAddress) && discoveredMap.TryGetValue(info.IpAddress, out var entryByIp))
            {
                device = entryByIp;
                device.MacAddress = mac;
            }
            else
            {
                device = new DiscoveredNetworkDevice
                {
                    MacAddress = mac,
                    IsReachable = true
                };
                if (!string.IsNullOrEmpty(info.IpAddress))
                {
                    discoveredMap[info.IpAddress] = device;
                }
                else
                {
                    discoveredMap[mac] = device;
                }
            }

            if (device != null)
            {
                device.Hostname = info.Identity;
                device.Vendor = "MikroTik";
                device.Platform = "RouterOS";
                device.Version = info.Version;
                device.Protocol = protocol;
                device.Interface = info.Interface;
                device.Uptime = info.Uptime;

                if (!string.IsNullOrEmpty(info.BoardName))
                {
                    device.Platform = $"RouterOS ({info.BoardName})";
                }
                if (!string.IsNullOrEmpty(info.IpAddress))
                {
                    device.IpAddress = info.IpAddress;
                }
            }
        }

        if (device != null)
        {
            onDeviceUpdated?.Invoke(device);
        }
        return device!;
    }

    private static DiscoveredNetworkDevice MergeAndNotifyUbnt(
        Dictionary<string, DiscoveredNetworkDevice> discoveredMap,
        UbntPacketInfo info,
        Action<DiscoveredNetworkDevice> onDeviceUpdated)
    {
        if (info == null || string.IsNullOrEmpty(info.MacAddress)) return null!;
        var mac = info.MacAddress.ToUpper();

        DiscoveredNetworkDevice? device = null;
        lock (discoveredMap)
        {
            var entryByMac = discoveredMap.Values.FirstOrDefault(d => d.MacAddress.Equals(mac, StringComparison.OrdinalIgnoreCase));
            if (entryByMac != null)
            {
                device = entryByMac;
            }
            else if (!string.IsNullOrEmpty(info.IpAddress) && discoveredMap.TryGetValue(info.IpAddress, out var entryByIp))
            {
                device = entryByIp;
                device.MacAddress = mac;
            }
            else
            {
                device = new DiscoveredNetworkDevice
                {
                    MacAddress = mac,
                    IsReachable = true
                };
                if (!string.IsNullOrEmpty(info.IpAddress))
                {
                    discoveredMap[info.IpAddress] = device;
                }
                else
                {
                    discoveredMap[mac] = device;
                }
            }

            if (device != null)
            {
                device.Hostname = info.Identity;
                device.Vendor = "Ubiquiti";
                device.Protocol = "UADP";
                device.Version = info.Version;
                device.Platform = info.BoardName;
                if (!string.IsNullOrEmpty(info.IpAddress))
                {
                    device.IpAddress = info.IpAddress;
                }
            }
        }

        if (device != null)
        {
            onDeviceUpdated?.Invoke(device);
        }
        return device!;
    }

    // ────────────────────────────────────────────────────────────────────
    // CRUD — الأجهزة المسجلة
    // ────────────────────────────────────────────────────────────────────

    public async Task<List<BroadcastingDevice>> GetAllDevicesAsync()
        => await _db.BroadcastingDevices.OrderBy(d => d.Vendor).ThenBy(d => d.DisplayName).ToListAsync();

    public async Task<BroadcastingDevice?> GetDeviceByIdAsync(Guid id)
        => await _db.BroadcastingDevices.FindAsync(id);

    public async Task<BroadcastingDevice> AddDeviceAsync(BroadcastingDevice device)
    {
        device.Id = Guid.NewGuid();
        device.CreatedAt = DateTime.UtcNow;
        _db.BroadcastingDevices.Add(device);
        await _db.SaveChangesAsync();
        return device;
    }

    public async Task<BroadcastingDevice> UpdateDeviceAsync(BroadcastingDevice device)
    {
        device.UpdatedAt = DateTime.UtcNow;
        _db.BroadcastingDevices.Update(device);
        await _db.SaveChangesAsync();
        return device;
    }

    public async Task DeleteDeviceAsync(Guid id)
    {
        var device = await _db.BroadcastingDevices.FindAsync(id);
        if (device != null)
        {
            _db.BroadcastingDevices.Remove(device);
            await _db.SaveChangesAsync();
        }
    }
}
