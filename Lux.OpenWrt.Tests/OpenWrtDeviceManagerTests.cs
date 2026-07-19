using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.OpenWrt.Models;
using Lux.OpenWrt.Services;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lux.OpenWrt.Tests;

public class OpenWrtDeviceManagerTests
{
    private readonly Mock<IUbusClient> _ubusClientMock;
    private readonly Mock<IUciService> _uciServiceMock;
    private readonly Mock<IDeviceDiscoveryService> _discoveryServiceMock;
    private readonly Mock<IBackupRestoreService> _backupRestoreServiceMock;
    private readonly Mock<ILogger<OpenWrtDeviceManager>> _loggerMock;
    private readonly OpenWrtDeviceManager _deviceManager;

    public OpenWrtDeviceManagerTests()
    {
        _ubusClientMock = new Mock<IUbusClient>();
        _uciServiceMock = new Mock<IUciService>();
        _discoveryServiceMock = new Mock<IDeviceDiscoveryService>();
        _backupRestoreServiceMock = new Mock<IBackupRestoreService>();
        _loggerMock = new Mock<ILogger<OpenWrtDeviceManager>>();

        _deviceManager = new OpenWrtDeviceManager(
            _ubusClientMock.Object,
            _uciServiceMock.Object,
            _discoveryServiceMock.Object,
            _backupRestoreServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DiscoverDeviceAsync_Success_ReturnsDevice()
    {
        // Arrange
        var host = "192.168.1.1";
        var user = "root";
        var pass = "password";
        var session = "test-session";
        var acls = DeviceAcls.FullPermissions();

        _ubusClientMock.Setup(u => u.LoginWithAclsAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync((session, acls));

        var device = new NetworkDevice { IpAddress = host, Vendor = Lux.Platform.Abstractions.DeviceVendor.OpenWrt };
        _discoveryServiceMock.Setup(d => d.DiscoverDeviceAsync(host, session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NetworkDevice>.Success(device));

        // Act
        var result = await _deviceManager.DiscoverDeviceAsync(host, user, pass);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(host, result.Value?.IpAddress);
        
        _ubusClientMock.Verify(u => u.LoginWithAclsAsync(host, user, pass, It.IsAny<CancellationToken>()), Times.Once);
        _discoveryServiceMock.Verify(d => d.DiscoverDeviceAsync(host, session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DiscoverDeviceAsync_LoginFails_ReturnsFailure()
    {
        // Arrange
        var host = "192.168.1.1";
        var user = "root";
        var pass = "wrong-password";

        _ubusClientMock.Setup(u => u.LoginWithAclsAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Invalid username or password"));

        // Act
        var result = await _deviceManager.DiscoverDeviceAsync(host, user, pass);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.Contains("Invalid username or password", result.ErrorMessage);
    }

    [Fact]
    public async Task DiscoverDeviceAsync_DiscoveryFails_ReturnsFailure()
    {
        // Arrange
        var host = "192.168.1.1";
        var user = "root";
        var pass = "password";
        var session = "test-session";
        var acls = DeviceAcls.FullPermissions();

        _ubusClientMock.Setup(u => u.LoginWithAclsAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync((session, acls));

        _discoveryServiceMock.Setup(d => d.DiscoverDeviceAsync(host, session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NetworkDevice>.Failure("Device timed out during discovery", ErrorType.ExternalService));

        // Act
        var result = await _deviceManager.DiscoverDeviceAsync(host, user, pass);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.ExternalService, result.ErrorType);
        Assert.Contains("Device timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task IsReachableAsync_ValidHost_ReturnsTrue_OrFalseIfActuallyOffline()
    {
        // Arrange
        var host = "127.0.0.1"; // Localhost should ping successfully on most test runners

        // Act
        var result = await _deviceManager.IsReachableAsync(host);

        // Assert
        // We can't strictly guarantee network condition of test runner, 
        // but it shouldn't throw exception. Usually it will be true for 127.0.0.1
        Assert.True(result);
    }

    [Fact]
    public async Task IsReachableAsync_InvalidHost_ReturnsFalse()
    {
        // Arrange
        var host = "256.256.256.256"; // Invalid IP address string, Ping should fail or throw

        // Act
        var result = await _deviceManager.IsReachableAsync(host);

        // Assert
        Assert.False(result);
    }
}
