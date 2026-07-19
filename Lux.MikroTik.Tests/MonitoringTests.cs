using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Interfaces;
using Lux.MikroTik.Models;
using Lux.MikroTik.Monitoring;
using Lux.MikroTik.Providers;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Domain.Entities;
using Moq;
using Xunit;

namespace Lux.MikroTik.Tests;

public class MonitoringTests
{
    [Fact]
    public async Task TelemetryProvider_GetTelemetryAsync_MapsDataCorrectly()
    {
        var mockProvider = new Mock<IRouterOsProvider>();
        mockProvider.Setup(p => p.IsConnected).Returns(true);
        
        mockProvider.Setup(p => p.ExecuteAsync(It.Is<MikroTikCommand>(c => c.Command == "/system/resource/print")))
            .ReturnsAsync(Result<MikroTikResponse>.Success(new MikroTikResponse
            {
                Success = true,
                RawData = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string>
                    {
                        { "cpu-load", "15" },
                        { "total-memory", "536870912" }, // 512 MB
                        { "free-memory", "268435456" }, // 256 MB
                        { "uptime", "1w2d3h4m5s" },
                        { "version", "7.20" }
                    }
                }
            }));

        mockProvider.Setup(p => p.ExecuteAsync(It.Is<MikroTikCommand>(c => c.Command == "/system/routerboard/print")))
            .ReturnsAsync(Result<MikroTikResponse>.Success(new MikroTikResponse
            {
                Success = true,
                RawData = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "board-name", "RB5009" } }
                }
            }));

        mockProvider.Setup(p => p.ExecuteAsync(It.Is<MikroTikCommand>(c => c.Command == "/interface/print")))
            .ReturnsAsync(Result<MikroTikResponse>.Success(new MikroTikResponse
            {
                Success = true,
                RawData = new List<Dictionary<string, string>> { new(), new() } // Count = 2
            }));

        mockProvider.Setup(p => p.ExecuteAsync(It.Is<MikroTikCommand>(c => c.Command == "/ip/hotspot/active/print")))
            .ReturnsAsync(Result<MikroTikResponse>.Success(new MikroTikResponse
            {
                Success = true,
                RawData = new List<Dictionary<string, string>> { new(), new(), new() } // Count = 3
            }));

        var telemetryProvider = new MikroTikTelemetryProvider(mockProvider.Object);
        var device = new NetworkDevice { Id = Guid.NewGuid() };

        var result = await telemetryProvider.GetTelemetryAsync(device, "session");

        Assert.True(result.IsSuccess);
        Assert.Equal(15, result.Value.CpuUsagePercent);
        Assert.Equal(512, result.Value.MemoryTotalMb);
        Assert.Equal(256, result.Value.MemoryUsedMb);
        Assert.Equal(50, result.Value.MemoryUsagePercent);
        Assert.Equal("7.20", result.Value.FirmwareVersion);
        Assert.Equal(3, result.Value.ConnectedClients);
        // Uptime: 1w (7d) + 2d = 9d, 3h, 4m, 5s
        Assert.Equal(new TimeSpan(9, 3, 4, 5), result.Value.Uptime);
    }

    [Fact]
    public async Task TelemetryProvider_GetTelemetryAsync_HandlesPartialFailure()
    {
        var mockProvider = new Mock<IRouterOsProvider>();
        mockProvider.Setup(p => p.IsConnected).Returns(true);
        
        // Success on resource
        mockProvider.Setup(p => p.ExecuteAsync(It.Is<MikroTikCommand>(c => c.Command == "/system/resource/print")))
            .ReturnsAsync(Result<MikroTikResponse>.Success(new MikroTikResponse
            {
                Success = true,
                RawData = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string>
                    {
                        { "cpu-load", "25" },
                        { "total-memory", "536870912" }, 
                        { "free-memory", "268435456" }
                    }
                }
            }));

        // Failure on others
        mockProvider.Setup(p => p.ExecuteAsync(It.Is<MikroTikCommand>(c => c.Command != "/system/resource/print")))
            .ReturnsAsync(Result<MikroTikResponse>.Failure("Failed", ErrorType.ExternalService));

        var telemetryProvider = new MikroTikTelemetryProvider(mockProvider.Object);
        var device = new NetworkDevice { Id = Guid.NewGuid() };

        var result = await telemetryProvider.GetTelemetryAsync(device, "session");

        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Value.CpuUsagePercent);
        Assert.Equal(0, result.Value.ConnectedClients); // Should default to 0 on failure
    }

    [Fact]
    public async Task MonitoringService_GetTelemetryAsync_UsesSessionAndProvider()
    {
        var mockSession = new Mock<IMikroTikSessionManager>();
        var mockProvider = new Mock<IMikroTikTelemetryProvider>();
        
        mockProvider.Setup(p => p.GetTelemetryAsync(It.IsAny<IDevice>(), It.IsAny<string>(), default))
            .ReturnsAsync(Result<DeviceTelemetry>.Success(new DeviceTelemetry { CpuUsagePercent = 99 }));

        var service = new MikroTikMonitoringService(mockProvider.Object, mockSession.Object);

        var result = await service.GetTelemetryAsync(Guid.NewGuid().ToString(), "192.168.88.1", "admin", "pass");

        Assert.True(result.IsSuccess);
        Assert.Equal(99, result.Value.CpuUsagePercent);
        
        mockSession.Verify(s => s.OpenSessionAsync(It.Is<MikroTikConnectionOptions>(o => o.Host == "192.168.88.1" && o.Username == "admin"), default), Times.Once);
        mockSession.Verify(s => s.CloseSessionAsync(default), Times.Once);
    }
}
