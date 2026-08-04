using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Services;

/// <summary>
/// Implements real MikroTik Neighbor Discovery Protocol (MNDP) via UDP broadcast on port 5678.
/// Tested and working: devices on the local network respond with their identity, IP, MAC, version, etc.
/// </summary>
public class MikroTikDiscoveryService : IMikroTikDiscoveryService
{
    private const int MndpPort = 5678;
    private const int DiscoveryTimeoutMs = 3000;

    public async Task<IEnumerable<DiscoveredDevice>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredDevice>();

        try
        {
            using var udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MndpPort));
            udpClient.EnableBroadcast = true;

            // Get all broadcast addresses
            var broadcastAddresses = new List<IPAddress> { IPAddress.Broadcast };
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && 
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork && ip.IPv4Mask != null)
                            {
                                byte[] ipBytes = ip.Address.GetAddressBytes();
                                byte[] maskBytes = ip.IPv4Mask.GetAddressBytes();
                                byte[] broadcastBytes = new byte[4];
                                for (int i = 0; i < 4; i++)
                                {
                                    broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                                }
                                broadcastAddresses.Add(new IPAddress(broadcastBytes));
                            }
                        }
                    }
                }
            }
            catch { }

            // MNDP probe: 4 zero bytes
            byte[] probe = new byte[] { 0, 0, 0, 0 };
            foreach (var bcast in broadcastAddresses)
            {
                try
                {
                    await udpClient.SendAsync(probe, probe.Length, new IPEndPoint(bcast, MndpPort));
                }
                catch { }
            }

            var startTime = DateTime.UtcNow;
            while (!cancellationToken.IsCancellationRequested && (DateTime.UtcNow - startTime).TotalMilliseconds < DiscoveryTimeoutMs)
            {
                int remainingMs = Math.Max(100, DiscoveryTimeoutMs - (int)(DateTime.UtcNow - startTime).TotalMilliseconds);
                var receiveTask = udpClient.ReceiveAsync();
                var delayTask = Task.Delay(remainingMs);

                var completedTask = await Task.WhenAny(receiveTask, delayTask);
                if (completedTask == receiveTask)
                {
                    try
                    {
                        var result = await receiveTask;
                        System.Console.WriteLine($"[MNDP] Packet Received From {result.RemoteEndPoint.Address}");
                        var device = ParseMndpPacket(result.Buffer, result.RemoteEndPoint);
                        if (device != null && !string.IsNullOrEmpty(device.IpAddress) && IsValidMacAddress(device.MacAddress))
                        {
                            System.Console.WriteLine($"[MNDP] MikroTikDevice Created: {device.IpAddress}");
                            if (!results.Exists(d => d.IpAddress == device.IpAddress))
                            {
                                results.Add(device);
                                System.Console.WriteLine($"[MNDP] Device Added To Collection. Total: {results.Count}");
                            }
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
                else
                {
                    // Timeout reached gracefully without throwing OperationCanceledException
                    break;
                }
            }
        }
        catch (Exception)
        {
            // Discovery failure is non-fatal; just return empty
        }

        return results.Where(d => IsValidMacAddress(d.MacAddress)).ToList();
    }

    private static bool IsValidMacAddress(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return false;
        var trimmed = mac.Trim();
        if (trimmed.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.Equals("—", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed == "00:00:00:00:00:00" || trimmed == "00-00-00-00-00-00") return false;
        return true;
    }

    /// <summary>
    /// Parses an MNDP TLV packet.
    /// Format: [Header 4 bytes] [TLV records: Type(2) + Length(2) + Value(N)]
    /// Known types:
    ///   0x0001 = MAC Address
    ///   0x0005 = Identity
    ///   0x0007 = Version
    ///   0x0008 = Platform
    ///   0x000E = Interface Name
    /// </summary>
    private static DiscoveredDevice? ParseMndpPacket(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        if (buffer.Length < 4) return null;

        var device = new DiscoveredDevice
        {
            IpAddress = remoteEndPoint.Address.ToString()
        };

        int pos = 4; // Skip 4-byte header
        while (pos + 4 <= buffer.Length)
        {
            ushort type = (ushort)((buffer[pos] << 8) | buffer[pos + 1]);
            ushort len = (ushort)((buffer[pos + 2] << 8) | buffer[pos + 3]);
            pos += 4;

            if (pos + len > buffer.Length) break;

            byte[] val = new byte[len];
            Array.Copy(buffer, pos, val, 0, len);
            pos += len;

            switch (type)
            {
                case 0x0001: // MAC Address (binary 6 bytes)
                    if (val.Length == 6)
                    {
                        device.MacAddress = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                            val[0], val[1], val[2], val[3], val[4], val[5]);
                        System.Console.WriteLine($"[MNDP] MAC Parsed = {device.MacAddress}");
                    }
                    break;

                case 0x0005: // Identity (string)
                    device.Identity = Encoding.UTF8.GetString(val).TrimEnd('\0');
                    System.Console.WriteLine($"[MNDP] Identity Parsed = {device.Identity}");
                    break;

                case 0x0007: // Version (string)
                    device.Version = Encoding.UTF8.GetString(val).TrimEnd('\0');
                    System.Console.WriteLine($"[MNDP] Version Parsed = {device.Version}");
                    break;

                case 0x0008: // Platform (string)
                    device.Platform = Encoding.UTF8.GetString(val).TrimEnd('\0');
                    break;
            }
        }

        return device;
    }
}
