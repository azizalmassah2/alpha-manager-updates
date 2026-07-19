using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.OpenWrt.Services;
using Lux.Platform.Abstractions;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lux.OpenWrt.Tests;

public class DeviceDiscoveryServiceTests
{
    private readonly Mock<IUbusClient> _ubusMock;
    private readonly Mock<IUciService> _uciMock;
    private readonly Mock<ILogger<DeviceDiscoveryService>> _loggerMock;
    private readonly DeviceDiscoveryService _service;

    public DeviceDiscoveryServiceTests()
    {
        _ubusMock = new Mock<IUbusClient>();
        _uciMock = new Mock<IUciService>();
        _loggerMock = new Mock<ILogger<DeviceDiscoveryService>>();
        _service = new DeviceDiscoveryService(_uciMock.Object, _ubusMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task DiscoverDeviceAsync_ValidDevice_ReturnsSuccess()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "valid-session";
        
        var boardJson = "{\"hostname\":\"OpenWrt\",\"model\":\"TP-Link TL-WR841N\",\"release\":{\"version\":\"21.02.0\"}}";
        var boardElement = JsonDocument.Parse(boardJson).RootElement;
        
        _ubusMock.Setup(u => u.CallAsync(ip, session, "system", "board", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(boardElement);

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, "wireless", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, "network", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());

        // Act
        var result = await _service.DiscoverDeviceAsync(ip, session);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("OpenWrt", result.Value.Name);
        Assert.Equal("TP-Link TL-WR841N", result.Value.Model);
        Assert.Equal("21.02.0", result.Value.FirmwareVersion);
        Assert.Equal(DeviceVendor.OpenWrt, result.Value.Vendor);
        Assert.Equal(DeviceStatus.Online, result.Value.Status);
    }

    [Fact]
    public async Task DiscoverDeviceAsync_OfflineDevice_ReturnsFailure()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "session";
        
        _ubusMock.Setup(u => u.CallAsync(ip, session, "system", "board", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("ط®ط·ط£ ظپظٹ ط§ظ„ط§طھطµط§ظ„ ط¨ط§ظ„ط´ط¨ظƒط©"));

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, "wireless", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("ط®ط·ط£ ظپظٹ ط§ظ„ط§طھطµط§ظ„ ط¨ط§ظ„ط´ط¨ظƒط©"));

        // Act
        var result = await _service.DiscoverDeviceAsync(ip, session);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.Contains("ظپط´ظ„ ظپظٹ ط§ظƒطھط´ط§ظپ ط¥ط¹ط¯ط§ط¯ط§طھ ط§ظ„ط¬ظ‡ط§ط²", result.ErrorMessage);
    }

    [Fact]
    public async Task DiscoverDeviceAsync_SessionExpired_ReturnsFailure()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "expired-session";
        
        _ubusMock.Setup(u => u.CallAsync(ip, session, "system", "board", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("ط®ط·ط£ UBUS JSON-RPC (ط±ظ…ط² 6): Permission Denied"));

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, "wireless", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("ط®ط·ط£ UBUS JSON-RPC (ط±ظ…ط² 6): Permission Denied"));

        // Act
        var result = await _service.DiscoverDeviceAsync(ip, session);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Permission Denied", result.ErrorMessage);
    }

    [Fact]
    public async Task DiscoverDeviceAsync_Timeout_ReturnsFailure()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "session";
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Simulate timeout
        
        _ubusMock.Setup(u => u.CallAsync(ip, session, "system", "board", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, "wireless", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _service.DiscoverDeviceAsync(ip, session, cts.Token);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.ExternalService, result.ErrorType);
        Assert.Contains("ط§ظ†طھظ‡طھ ظ…ظ‡ظ„ط© ط§ط³طھظƒط´ط§ظپ ط§ظ„ط¬ظ‡ط§ط²", result.ErrorMessage);
    }

    [Fact]
    public async Task DiscoverDeviceAsync_InsufficientAcl_ContinuesWithDefaults()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "session";
        
        // System board fails due to ACL
        _ubusMock.Setup(u => u.CallAsync(ip, session, "system", "board", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Permission Denied"));

        // But UCI succeeds
        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, "wireless", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());
        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, "network", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());

        // Act
        var result = await _service.DiscoverDeviceAsync(ip, session);

        // Assert
        // The service catches system board exception and continues!
        Assert.True(result.IsSuccess);
        Assert.Equal("OpenWrt Router", result.Value.Name); // Fallback name
    }
}
