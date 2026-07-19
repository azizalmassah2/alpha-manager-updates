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
    private const int DiscoveryTimeoutMs = 4000;

    public async Task<IEnumerable<DiscoveredDevice>> DiscoverDevicesAsync()
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

            using var cts = new CancellationTokenSource(DiscoveryTimeoutMs);
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var result = await udpClient.ReceiveAsync(cts.Token);
                    System.Console.WriteLine($"[MNDP] Packet Received From {result.RemoteEndPoint.Address}");
                    var device = ParseMndpPacket(result.Buffer, result.RemoteEndPoint);
                    if (device != null && !string.IsNullOrEmpty(device.IpAddress))
                    {
                        System.Console.WriteLine($"[MNDP] MikroTikDevice Created: {device.IpAddress}");
                        // Avoid duplicates
                        if (!results.Exists(d => d.IpAddress == device.IpAddress))
                        {
                            results.Add(device);
                            System.Console.WriteLine($"[MNDP] Device Added To Collection. Total: {results.Count}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout reached — normal exit
            }
        }
        catch (Exception)
        {
            // Discovery failure is non-fatal; just return empty
        }

        return results;
    }

    /// <summary>
    /// Parses an MNDP TLV packet.
    /// Format: [Header 4 bytes] [TLV records: Type(2) + Length(2) + Value(N)]
    /// Known types:
    ///   0x0001 = MAC Address
    ///   0x0005 = Identity
    ///   0x0007 = Version
    ///   0x0008 = Platform
    ///   0x000A = Uptime (seconds, uint32 LE)
    ///   0x000B = RouterBoard
    ///   0x000C = Board Name
    ///   0x0010 = InterfaceName
    ///   0x0011 = IPv4 Address
    /// </summary>
    private static DiscoveredDevice? ParseMndpPacket(byte[] data, IPEndPoint sender)
    {
        if (data.Length < 4) return null;

        // Skip 4-byte header (version + sequence + checksum)
        int offset = 4;
        var device = new DiscoveredDevice
        {
            IpAddress = sender.Address.ToString()
        };

        while (offset + 4 <= data.Length)
        {
            ushort type = (ushort)((data[offset] << 8) | data[offset + 1]);
            ushort length = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
            offset += 4;

            if (offset + length > data.Length) break;

            byte[] value = new byte[length];
            Array.Copy(data, offset, value, 0, length);
            offset += length;

            switch (type)
            {
                case 0x0001: // MAC Address
                    if (length == 6)
                        device.MacAddress = BitConverter.ToString(value).Replace("-", ":");
                    System.Console.WriteLine($"[MNDP] MAC Parsed = {device.MacAddress}");
                    break;

                case 0x0005: // Identity
                    device.Identity = Encoding.UTF8.GetString(value).TrimEnd('\0');
                    System.Console.WriteLine($"[MNDP] Identity Parsed = {device.Identity}");
                    break;

                case 0x0007: // RouterOS Version
                    device.Version = Encoding.UTF8.GetString(value).TrimEnd('\0');
                    System.Console.WriteLine($"[MNDP] Version Parsed = {device.Version}");
                    break;

                case 0x0008: // Platform
                    device.Platform = Encoding.UTF8.GetString(value).TrimEnd('\0');
                    break;

                case 0x000A: // Uptime in seconds (uint32 LE)
                    if (length == 4)
                    {
                        uint uptimeSecs = BitConverter.ToUInt32(value, 0);
                        var ts = TimeSpan.FromSeconds(uptimeSecs);
                        device.Uptime = $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
                    }
                    break;

                case 0x000B: // RouterBoard model
                    device.RouterBoard = Encoding.UTF8.GetString(value).TrimEnd('\0');
                    break;

                case 0x0011: // IPv4 Address (4 bytes)
                    if (length == 4)
                        device.IpAddress = $"{value[0]}.{value[1]}.{value[2]}.{value[3]}";
                    break;
            }
        }
        if (string.IsNullOrWhiteSpace(device.MacAddress))
            return null;

        return device;
    }
}
