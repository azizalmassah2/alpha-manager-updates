using System.Threading.Tasks;
using Lux.MikroTik.Discovery;
using Lux.MikroTik.Models;
using Lux.MikroTik.Providers;
using Lux.MikroTik.Interfaces;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using Moq;
using Xunit;
using Lux.Platform.Abstractions;

namespace Lux.MikroTik.Tests;

public class DiscoveryTests
{
    [Fact]
    public async Task DeviceInfoProvider_GetDeviceInfoAsync_ReturnsMockData()
    {
        var mockProvider = new Mock<IRouterOsProvider>();
        mockProvider.Setup(p => p.ExecuteAsync(It.IsAny<MikroTikCommand>()))
                    .ReturnsAsync(Result<MikroTikResponse>.Success(new MikroTikResponse { Success = true }));

        var infoProvider = new MikroTikDeviceInfoProvider(mockProvider.Object);
        var mockDevice = new Mock<IDevice>();

        var result = await infoProvider.GetDeviceInfoAsync(mockDevice.Object);

        Assert.True(result.IsSuccess);
        Assert.Equal("MikroTik-Test", result.Value.Identity);
        Assert.Equal("RB5009", result.Value.Model);
        Assert.Equal("arm64", result.Value.Architecture);
        Assert.Equal("7.20", result.Value.FirmwareVersion);
    }

    [Fact]
    public async Task DiscoveryService_DiscoverAsync_MapsToNetworkDeviceCorrectly()
    {
        var mockInfoProvider = new Mock<IMikroTikDeviceInfoProvider>();
        mockInfoProvider.Setup(p => p.GetDeviceInfoAsync(It.IsAny<IDevice>(), default))
                        .ReturnsAsync(Result<MikroTikDeviceInfo>.Success(new MikroTikDeviceInfo
                        {
                            Identity = "Mapped-Test",
                            FirmwareVersion = "7.20",
                            Model = "RB5009"
                        }));

        var discoveryService = new MikroTikDiscoveryService(mockInfoProvider.Object);
        
        var mockDevice = new Mock<IDevice>();
        mockDevice.Setup(d => d.IpAddress).Returns("192.168.1.1");

        var result = await discoveryService.DiscoverAsync(mockDevice.Object);

        Assert.True(result.IsSuccess);
        Assert.Equal(DeviceVendor.MikroTik, result.Value.Vendor);
        Assert.Equal(DeviceStatus.Online, result.Value.Status);
        Assert.Equal("Mapped-Test", result.Value.Name);
        Assert.Equal("192.168.1.1", result.Value.IpAddress);
        Assert.Equal("7.20", result.Value.FirmwareVersion);
    }
}
