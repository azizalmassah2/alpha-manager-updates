using System;
using System.Text.Json;
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

public class ProgrammingServiceTests
{
    private readonly Mock<IOpenWrtDeviceManager> _deviceManagerMock;
    private readonly Mock<IUbusClient> _ubusMock;
    private readonly Mock<IHostnameConfigurationService> _hostnameMock;
    private readonly Mock<INetworkConfigurationService> _networkMock;
    private readonly Mock<IVlanConfigurationService> _vlanMock;
    private readonly Mock<IWirelessConfigurationService> _wirelessMock;
    private readonly Mock<ICommitApplyService> _commitApplyMock;
    private readonly Mock<IProgrammingRollbackService> _rollbackMock;
    private readonly Mock<ILogger<ProgrammingService>> _loggerMock;
    
    private readonly ProgrammingService _service;

    public ProgrammingServiceTests()
    {
        _deviceManagerMock = new Mock<IOpenWrtDeviceManager>();
        _ubusMock = new Mock<IUbusClient>();
        _hostnameMock = new Mock<IHostnameConfigurationService>();
        _networkMock = new Mock<INetworkConfigurationService>();
        _vlanMock = new Mock<IVlanConfigurationService>();
        _wirelessMock = new Mock<IWirelessConfigurationService>();
        _commitApplyMock = new Mock<ICommitApplyService>();
        _rollbackMock = new Mock<IProgrammingRollbackService>();
        _loggerMock = new Mock<ILogger<ProgrammingService>>();

        _service = new ProgrammingService(
            _deviceManagerMock.Object,
            _ubusMock.Object,
            _hostnameMock.Object,
            _networkMock.Object,
            _vlanMock.Object,
            _wirelessMock.Object,
            _commitApplyMock.Object,
            _rollbackMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task ProgramDeviceAsync_SuccessFlow_CallsAllServices()
    {
        // Arrange
        var host = "192.168.1.1";
        var targetIp = "10.0.0.1";
        var user = "root";
        var pass = "password";
        var session = "test-session";
        var acls = DeviceAcls.FullPermissions();

        _ubusMock.Setup(u => u.LoginAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var device = new NetworkDevice 
        { 
            IpAddress = host, 
            Metadata = JsonSerializer.Serialize(new {
                LanSectionName = "lan",
                LanDeviceName = "br-lan",
                VlanType = "Traditional",
                SwitchName = "switch0",
                SwitchCpuPort = "0t",
                SwitchLanPorts = "1 2 3 4",
                Radio24GhzName = "radio0",
                Radio5GhzName = "radio1",
                WifiIface24GhzSection = "default_radio0",
                WifiIface5GhzSection = "default_radio1"
            })
        };

        _deviceManagerMock.Setup(d => d.DiscoverDeviceAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NetworkDevice>.Success(device));

        var progressMock = new Mock<IProgress<ProgrammingProgress>>();

        var wirelessConfig = new WirelessConfig { Mode = WirelessMode.AccessPoint, Ssid24Ghz = "WIFI24", Ssid5Ghz = "WIFI5" };

        // Act
        var result = await _service.ProgramDeviceAsync(host, user, pass, targetIp, "10.0.0.254", "255.255.255.0", 100, wirelessConfig, progressMock.Object);

        // Assert
        Assert.True(result.IsSuccess);
        
        _hostnameMock.Verify(h => h.ConfigureHostnameAsync(host, session, targetIp, It.IsAny<CancellationToken>()), Times.Once);
        _networkMock.Verify(n => n.SetLanIpAsync(host, session, "lan", targetIp, "10.0.0.254", "255.255.255.0", It.IsAny<CancellationToken>()), Times.Once);
        _vlanMock.Verify(v => v.CreateVlanAsync(host, session, "br-lan", "Traditional", 100, "switch0", "0t", "1 2 3 4", It.IsAny<CancellationToken>()), Times.Once);
        _wirelessMock.Verify(w => w.ConfigureRadioApAsync(host, session, "radio0", "default_radio0", "WIFI24", It.IsAny<string>(), "vlan100", It.IsAny<CancellationToken>()), Times.Once);
        _commitApplyMock.Verify(c => c.CommitAndApplyAsync(host, session, true, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProgramDeviceAsync_AclInsufficient_ReturnsFailure()
    {
        // Arrange
        var host = "192.168.1.1";
        var user = "root";
        var pass = "password";
        var session = "test-session";
        var acls = new DeviceAcls { CanGet = true, CanSet = false }; // Missing CanSet

        _ubusMock.Setup(u => u.LoginWithAclsAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync((session, acls));

        var progressMock = new Mock<IProgress<ProgrammingProgress>>();

        // Act
        var result = await _service.ProgramDeviceAsync(host, user, pass, "10.0.0.1", "", "", 100, new WirelessConfig(), progressMock.Object, canCommit: false, canApply: false);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
        Assert.Contains("ظ„ط§ ظٹظ…ظ†ط­ ط§ظ„ط­ط¯ ط§ظ„ط£ط¯ظ†ظ‰ ظ…ظ† ط§ظ„طµظ„ط§ط­ظٹط§طھ", result.ErrorMessage);
    }

    [Fact]
    public async Task ProgramDeviceAsync_DiscoveryFails_ReturnsFailureAndDoesNotProgram()
    {
        // Arrange
        var host = "192.168.1.1";
        var user = "root";
        var pass = "password";
        var session = "test-session";

        _ubusMock.Setup(u => u.LoginAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _deviceManagerMock.Setup(d => d.DiscoverDeviceAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NetworkDevice>.Failure("Device timed out", ErrorType.ExternalService));

        var progressMock = new Mock<IProgress<ProgrammingProgress>>();

        // Act
        var result = await _service.ProgramDeviceAsync(host, user, pass, "10.0.0.1", "", "", 100, new WirelessConfig(), progressMock.Object);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.ExternalService, result.ErrorType);
        _hostnameMock.Verify(h => h.ConfigureHostnameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProgramDeviceAsync_ExceptionDuringProgramming_TriggersRollback()
    {
        // Arrange
        var host = "192.168.1.1";
        var user = "root";
        var pass = "password";
        var session = "test-session";

        _ubusMock.Setup(u => u.LoginAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var device = new NetworkDevice 
        { 
            IpAddress = host, 
            Metadata = JsonSerializer.Serialize(new {
                LanSectionName = "lan",
                LanDeviceName = "br-lan",
                VlanType = "Traditional",
                SwitchName = "switch0",
                SwitchCpuPort = "0t",
                SwitchLanPorts = "1 2 3 4",
                Radio24GhzName = "radio0",
                Radio5GhzName = "radio1",
                WifiIface24GhzSection = "default_radio0",
                WifiIface5GhzSection = "default_radio1"
            })
        };

        _deviceManagerMock.Setup(d => d.DiscoverDeviceAsync(host, user, pass, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NetworkDevice>.Success(device));

        _hostnameMock.Setup(h => h.ConfigureHostnameAsync(host, session, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network failure simulated"));

        var progressMock = new Mock<IProgress<ProgrammingProgress>>();

        // Act
        var result = await _service.ProgramDeviceAsync(host, user, pass, "10.0.0.1", "", "", 100, new WirelessConfig(), progressMock.Object);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Network failure simulated", result.ErrorMessage);
        _rollbackMock.Verify(r => r.RollbackAsync(host, session, It.IsAny<CancellationToken>()), Times.Once);
    }
}
