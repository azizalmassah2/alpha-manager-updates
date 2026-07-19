using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.OpenWrt.Models;
using Lux.OpenWrt.Services;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lux.OpenWrt.Tests;

public class DeviceMonitoringServiceTests
{
    private readonly Mock<IUbusClient> _ubusClientMock;
    private readonly Mock<IOpenWrtTelemetryProvider> _telemetryProviderMock;
    private readonly Mock<ILogger<DeviceMonitoringService>> _loggerMock;
    private readonly DeviceMonitoringService _service;

    public DeviceMonitoringServiceTests()
    {
        _ubusClientMock = new Mock<IUbusClient>();
        _telemetryProviderMock = new Mock<IOpenWrtTelemetryProvider>();
        _loggerMock = new Mock<ILogger<DeviceMonitoringService>>();
        _service = new DeviceMonitoringService(_ubusClientMock.Object, _telemetryProviderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetTelemetryAsync_Success_ReturnsTelemetry()
    {
        // Arrange
        var host = "192.168.1.1";
        var deviceId = "dev-1";
        var session = "valid-session";
        var acls = DeviceAcls.FullPermissions();

        _ubusClientMock.Setup(u => u.LoginWithAclsAsync(host, "root", "pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync((session, acls));

        var telemetry = new Lux.Platform.Abstractions.Models.DeviceTelemetry { DeviceId = deviceId, Status = "Online" };
        _telemetryProviderMock.Setup(t => t.GetTelemetryAsync(deviceId, host, session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Lux.Platform.Abstractions.Common.Result<Lux.Platform.Abstractions.Models.DeviceTelemetry>.Success(telemetry));

        // Act
        var result = await _service.GetTelemetryAsync(deviceId, host, "root", "pass");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Online", result.Value?.Status);
    }

    [Fact]
    public async Task GetTelemetryAsync_DeviceOffline_ReturnsFailure()
    {
        // Arrange
        _ubusClientMock.Setup(u => u.LoginWithAclsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network unreachable"));

        // Act
        var result = await _service.GetTelemetryAsync("dev-1", "192.168.1.1", "root", "pass");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.Contains("Device Offline or Unreachable", result.ErrorMessage);
    }

    [Fact]
    public async Task GetTelemetryAsync_InvalidSession_ReturnsFailure()
    {
        // Arrange
        _ubusClientMock.Setup(u => u.LoginWithAclsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("", new DeviceAcls()));

        // Act
        var result = await _service.GetTelemetryAsync("dev-1", "192.168.1.1", "root", "pass");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
        Assert.Contains("Invalid Session", result.ErrorMessage);
    }

    [Fact]
    public async Task GetTelemetryAsync_Timeout_ReturnsFailure()
    {
        // Arrange
        _ubusClientMock.Setup(u => u.LoginWithAclsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Timeout"));

        // Act
        var result = await _service.GetTelemetryAsync("dev-1", "192.168.1.1", "root", "pass");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.ExternalService, result.ErrorType);
        Assert.Contains("Timeout", result.ErrorMessage);
    }
}

public class OpenWrtTelemetryProviderTests
{
    private readonly Mock<IUbusClient> _ubusClientMock;
    private readonly Mock<ILogger<OpenWrtTelemetryProvider>> _loggerMock;
    private readonly OpenWrtTelemetryProvider _provider;

    public OpenWrtTelemetryProviderTests()
    {
        _ubusClientMock = new Mock<IUbusClient>();
        _loggerMock = new Mock<ILogger<OpenWrtTelemetryProvider>>();
        _provider = new OpenWrtTelemetryProvider(_ubusClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetTelemetryAsync_PartialData_ReturnsWithPartialStatus()
    {
        // Arrange
        var host = "192.168.1.1";
        var session = "session";
        
        // Throw exception on system info to simulate partial data
        _ubusClientMock.Setup(u => u.CallAsync(host, session, "system", "info", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("System call failed"));

        _ubusClientMock.Setup(u => u.CallAsync(host, session, "system", "board", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{\"release\":{\"description\":\"OpenWrt 23.05.0\"}}").RootElement);

        _ubusClientMock.Setup(u => u.CallAsync(host, session, "iwinfo", "devices", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("[]").RootElement);

        // Act
        var result = await _provider.GetTelemetryAsync("dev-1", host, session);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("PartialData", result.Value?.Status);
        Assert.Equal("OpenWrt 23.05.0", result.Value?.FirmwareVersion);
    }
}
