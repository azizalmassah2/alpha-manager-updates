using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Discovery Started");
        
        using var udpClient = new UdpClient();
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        
        try
        {
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 5678));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception binding to 5678: {ex.Message}");
            return;
        }

        udpClient.EnableBroadcast = true;
        var broadcastAddresses = new List<IPAddress> { IPAddress.Broadcast };
        
        var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
        Console.WriteLine($"Network Interfaces Found: {interfaces.Length}");

        foreach (var ni in interfaces)
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
                        var bcast = new IPAddress(broadcastBytes);
                        broadcastAddresses.Add(bcast);
                        
                        Console.WriteLine($"- Selected Interface: {ni.Name}");
                        Console.WriteLine($"  Local IP: {ip.Address}");
                        Console.WriteLine($"  Subnet Mask: {ip.IPv4Mask}");
                        Console.WriteLine($"  Broadcast Address: {bcast}");
                    }
                }
            }
        }

        byte[] probe = new byte[] { 0, 0, 0, 0 };
        foreach (var bcast in broadcastAddresses)
        {
            try
            {
                await udpClient.SendAsync(probe, probe.Length, new IPEndPoint(bcast, 5678));
                Console.WriteLine($"Discovery Packet Sent to {bcast}:5678");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to {bcast}: {ex.Message}");
            }
        }

        Console.WriteLine("Waiting for responses...");
        using var cts = new CancellationTokenSource(4000);
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await udpClient.ReceiveAsync(cts.Token);
                Console.WriteLine("-------------------------");
                Console.WriteLine($"Response Received!");
                Console.WriteLine($"Response Source IP: {result.RemoteEndPoint.Address}");
                Console.WriteLine($"Data Length: {result.Buffer.Length} bytes");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Timeout");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }
}
