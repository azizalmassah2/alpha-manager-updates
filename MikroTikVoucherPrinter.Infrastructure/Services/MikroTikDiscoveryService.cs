using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// موديل بيانات جهاز المايكروتك المكتشف
/// </summary>
public class MikroTikDeviceModel
{
    public string MacAddress { get; set; } = "Unknown";
    public string IpAddress { get; set; } = "0.0.0.0";
    public string Identity { get; set; } = "MikroTik";
    public string Version { get; set; } = "Unknown";
    public string Board { get; set; } = "Unknown";
    public string Uptime { get; set; } = "00:00:00";
}

/// <summary>
/// محرك اكتشاف أجهزة المايكروتك على الشبكة باستخدام MNDP (MikroTik Neighbor Discovery Protocol)
/// </summary>
public class MikroTikDiscoveryService
{
    public async Task<List<MikroTikDeviceModel>> DiscoverAsync(int timeoutMs = 3000, CancellationToken cancellationToken = default)
    {
        var devices = new List<MikroTikDeviceModel>();
        var seenMacs = new HashSet<string>();

        using var udpClient = new UdpClient();
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.ExclusiveAddressUse = false;
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 5678));
        udpClient.EnableBroadcast = true;
        udpClient.Client.ReceiveTimeout = timeoutMs;

        var request = new byte[] { 0, 0, 0, 0 };
        var endpoints = new HashSet<IPEndPoint>();
        endpoints.Add(new IPEndPoint(IPAddress.Broadcast, 5678));

        // الحصول على جميع عناوين البث (Broadcast) لجميع كروت الشبكة
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && 
                    ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                {
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && ua.IPv4Mask != null)
                        {
                            var ipBytes = ua.Address.GetAddressBytes();
                            var maskBytes = ua.IPv4Mask.GetAddressBytes();
                            var broadcastBytes = new byte[4];
                            for (int i = 0; i < 4; i++)
                            {
                                broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                            }
                            endpoints.Add(new IPEndPoint(new IPAddress(broadcastBytes), 5678));
                        }
                    }
                }
            }
        }
        catch { /* تجاهل أي أخطاء في قراءة كروت الشبكة */ }

        // إرسال طلب الاكتشاف لجميع العناوين
        foreach (var ep in endpoints)
        {
            try { await udpClient.SendAsync(request, request.Length, ep); } catch { }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            while (!cts.IsCancellationRequested)
            {
                var receiveResult = await udpClient.ReceiveAsync(cts.Token);
                var buffer = receiveResult.Buffer;

                if (buffer.Length < 4) continue;

                var device = ParseMndpPayload(buffer);
                device.IpAddress = receiveResult.RemoteEndPoint.Address.ToString();

                if (!string.IsNullOrEmpty(device.MacAddress) && 
                    !device.MacAddress.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && 
                    !device.MacAddress.Equals("—", StringComparison.OrdinalIgnoreCase) && 
                    device.MacAddress != "00:00:00:00:00:00" && 
                    device.MacAddress != "00-00-00-00-00-00" && 
                    seenMacs.Add(device.MacAddress))
                {
                    devices.Add(device);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }

        return devices.OrderBy(d => d.Identity).ToList();
    }

    /// <summary>
    /// قراءة حِزم بيانات بروتوكول MNDP والمكونة من نظام TLV (Type-Length-Value)
    /// </summary>
    private MikroTikDeviceModel ParseMndpPayload(byte[] buffer)
    {
        var device = new MikroTikDeviceModel();
        int offset = 4; // تخطي الترويسة

        while (offset + 4 <= buffer.Length)
        {
            int type = (buffer[offset] << 8) | buffer[offset + 1];
            int length = (buffer[offset + 2] << 8) | buffer[offset + 3];

            offset += 4;

            if (offset + length > buffer.Length) break;

            byte[] valueBytes = new byte[length];
            Array.Copy(buffer, offset, valueBytes, 0, length);

            switch (type)
            {
                case 1: // MAC Address
                    if (length >= 6)
                        device.MacAddress = string.Join(":", valueBytes.Take(6).Select(b => b.ToString("X2")));
                    break;
                case 5: // Identity
                    device.Identity = Encoding.ASCII.GetString(valueBytes);
                    break;
                case 7: // RouterOS Version
                    device.Version = Encoding.ASCII.GetString(valueBytes);
                    break;
                case 10: // Uptime
                    if (length == 4)
                    {
                        uint uptimeSec = (uint)((valueBytes[0] << 24) | (valueBytes[1] << 16) | (valueBytes[2] << 8) | valueBytes[3]);
                        var t = TimeSpan.FromSeconds(uptimeSec);
                        device.Uptime = $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
                    }
                    break;
                case 12: // Board Name
                    device.Board = Encoding.ASCII.GetString(valueBytes);
                    break;
            }

            offset += length;
        }

        return device;
    }
}
