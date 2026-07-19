using System;
using Xunit;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

namespace Lux.Management.Console.Tests;

public class NocViewModelTests
{
    [Theory]
    [InlineData("192.168.12.55", "192.168.12.1/24", true)]
    [InlineData("192.168.12.1", "192.168.12.1/24", true)]
    [InlineData("192.168.12.254", "192.168.12.0/24", true)]
    [InlineData("192.168.13.55", "192.168.12.1/24", false)]
    [InlineData("10.0.0.5", "10.0.0.0/8", true)]
    [InlineData("172.16.5.14", "172.16.5.0/28", true)] // Range: 172.16.5.0 - 172.16.5.15
    [InlineData("172.16.5.20", "172.16.5.0/28", false)]
    [InlineData("", "192.168.12.1/24", false)]
    [InlineData("192.168.12.55", "", false)]
    [InlineData("invalid-ip", "192.168.12.1/24", false)]
    [InlineData("192.168.12.55", "invalid-subnet", false)]
    public void TestIsIpInSubnet(string ip, string subnet, bool expected)
    {
        bool result = NocViewModel.IsIpInSubnet(ip, subnet);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestVlanMonitorItem_InitialFormatting()
    {
        var item = new VlanMonitorItem { Name = "VLAN12" };
        
        // Assert uninitialized measurements return "--"
        Assert.Equal("--", item.DownloadSpeedText);
        Assert.Equal("--", item.UploadSpeedText);
        Assert.Equal("--", item.UtilizationText);
        Assert.Equal("--", item.ConnectedClientsText);
        Assert.Equal("░░░░░░░░░░", item.LoadBlocks);
        Assert.Equal("--", item.TotalDownloadText);
        Assert.Equal("--", item.TotalUploadText);

        // Peak stats also return "--" when 0
        Assert.Equal("--", item.PeakDownloadSpeedText);
        Assert.Equal("--", item.PeakUploadSpeedText);
        Assert.Equal("--", item.PeakClientsText);
    }

    [Fact]
    public void TestVlanMonitorItem_WithMeasurements()
    {
        var item = new VlanMonitorItem 
        { 
            Name = "VLAN12",
            LastByteTime = DateTime.Now,
            DownloadSpeedMbps = 12.34,
            UploadSpeedMbps = 4.56,
            UtilizationPercent = 53.0,
            ConnectedClients = 15,
            PeakClients = 25,
            PeakDownloadSpeedMbps = 30.0,
            PeakUploadSpeedMbps = 10.0,
            LastTxBytes = 157286400, // 150 MB (Upload for client / Tx)
            LastRxBytes = 52428800,  // 50 MB (Download for client / Rx)
            HealthStatus = VlanHealthStatus.Busy
        };

        // Assert formatted strings
        Assert.Equal("12.34 Mbps", item.DownloadSpeedText);
        Assert.Equal("4.56 Mbps", item.UploadSpeedText);
        Assert.Equal("53.0%", item.UtilizationText);
        Assert.Equal("15", item.ConnectedClientsText);
        Assert.Equal("50.00 MB", item.TotalDownloadText);
        Assert.Equal("150.00 MB", item.TotalUploadText);

        // Peak values
        Assert.Equal("30.00 Mbps", item.PeakDownloadSpeedText);
        Assert.Equal("10.00 Mbps", item.PeakUploadSpeedText);
        Assert.Equal("25", item.PeakClientsText);

        // Load blocks for 53% -> Round(5.3) = 5 filled blocks
        Assert.Equal("█████░░░░░", item.LoadBlocks);
    }

    [Fact]
    public void TestVlanMonitorItem_HealthMonitoringFormatting()
    {
        var item = new VlanMonitorItem { Name = "VLAN10" };

        // Defaults
        Assert.Equal("Offline", item.DeviceStatus);
        Assert.Equal(0, item.LatencyMs);
        Assert.Null(item.LastSeen);
        Assert.Equal("--", item.LatencyText);
        Assert.Equal("--", item.LastSeenText);

        // Healthy
        item.DeviceStatus = "Healthy";
        item.LatencyMs = 25.4;
        var now = DateTime.Now;
        item.LastSeen = now;
        Assert.Equal("25.4 ms", item.LatencyText);
        Assert.Equal(now.ToString("yyyy-MM-dd HH:mm:ss"), item.LastSeenText);

        // Offline but latency has a value (should display "--")
        item.DeviceStatus = "Offline";
        Assert.Equal("--", item.LatencyText);
    }
}
