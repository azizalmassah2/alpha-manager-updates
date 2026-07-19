using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class UnifiedFirmwareServiceTests
{
    private readonly Mock<IDeviceFirmwareProvider> _providerMock;
    private readonly Mock<IUnifiedBackupService> _backupServiceMock;
    private readonly Mock<IFileTransferService> _fileTransferMock;
    private readonly Mock<IReconnectStrategy> _reconnectStrategyMock;
    private readonly UnifiedFirmwareService _service;
    private readonly Mock<IDevice> _deviceMock;
    private readonly FirmwareImage _image;

    public UnifiedFirmwareServiceTests()
    {
        _providerMock = new Mock<IDeviceFirmwareProvider>();
        _backupServiceMock = new Mock<IUnifiedBackupService>();
        _fileTransferMock = new Mock<IFileTransferService>();
        _reconnectStrategyMock = new Mock<IReconnectStrategy>();
        _deviceMock = new Mock<IDevice>();
        
        _deviceMock.Setup(d => d.Id).Returns("dev-1");
        _deviceMock.Setup(d => d.Vendor).Returns(DeviceVendor.OpenWrt);
        
        _providerMock.Setup(p => p.CanHandle(It.IsAny<IDevice>())).Returns(true);
        _providerMock.Setup(p => p.GetCurrentVersionAsync(It.IsAny<IDevice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("22.03.5"));

        _fileTransferMock.Setup(f => f.UploadAsync(It.IsAny<IDevice>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _reconnectStrategyMock.Setup(r => r.WaitForReconnectAsync(It.IsAny<IDevice>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _service = new UnifiedFirmwareService(
            new[] { _providerMock.Object },
            _backupServiceMock.Object,
            _fileTransferMock.Object,
            _reconnectStrategyMock.Object,
            NullLogger<UnifiedFirmwareService>.Instance);

        _image = new FirmwareImage
        {
            Name = "openwrt-23.05.bin",
            Version = "23.05.3"
        };
    }

    [Fact]
    public async Task UpgradeFirmwareAsync_Fails_IfValidationFails()
    {
        // Arrange
        _providerMock.Setup(p => p.ValidateFirmwareAsync(_deviceMock.Object, _image, default))
            .ReturnsAsync(Result<FirmwareCompatibilityResult>.Success(new FirmwareCompatibilityResult { IsCompatible = false, Error = "Incompatible" }));

        // Act
        var result = await _service.UpgradeFirmwareAsync(_deviceMock.Object, _image);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Success);
        Assert.Contains("Incompatible", result.Value.Error, StringComparison.OrdinalIgnoreCase);
        
        _backupServiceMock.Verify(b => b.CreateBackupAsync(It.IsAny<IDevice>(), It.IsAny<BackupType>(), It.IsAny<CancellationToken>()), Times.Never);
        _providerMock.Verify(p => p.UpgradeAsync(It.IsAny<IDevice>(), It.IsAny<FirmwareImage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpgradeFirmwareAsync_Proceeds_EvenIfBackupFails()
    {
        // Arrange
        _providerMock.Setup(p => p.ValidateFirmwareAsync(_deviceMock.Object, _image, default))
            .ReturnsAsync(Result<FirmwareCompatibilityResult>.Success(new FirmwareCompatibilityResult { IsCompatible = true }));
            
        _backupServiceMock.Setup(b => b.CreateBackupAsync(_deviceMock.Object, BackupType.Firmware, default))
            .ReturnsAsync(Result<DeviceBackup>.Failure("Backup failed", ErrorType.Unexpected));

        _providerMock.Setup(p => p.UpgradeAsync(_deviceMock.Object, _image, default))
            .ReturnsAsync(Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult
            {
                Success = true
            }));

        _providerMock.SetupSequence(p => p.GetCurrentVersionAsync(_deviceMock.Object, default))
            .ReturnsAsync(Result<string>.Success("22.03.5")) // Before
            .ReturnsAsync(Result<string>.Success("23.05.3")); // After

        // Act
        var result = await _service.UpgradeFirmwareAsync(_deviceMock.Object, _image);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.Equal("23.05.3", result.Value.NewVersion);
        
        _providerMock.Verify(p => p.UpgradeAsync(_deviceMock.Object, _image, default), Times.Once);
    }

    [Fact]
    public async Task UpgradeFirmwareAsync_ReturnsSuccess_WhenAllStepsSucceed()
    {
        // Arrange
        _providerMock.Setup(p => p.ValidateFirmwareAsync(_deviceMock.Object, _image, default))
            .ReturnsAsync(Result<FirmwareCompatibilityResult>.Success(new FirmwareCompatibilityResult { IsCompatible = true }));
            
        _backupServiceMock.Setup(b => b.CreateBackupAsync(_deviceMock.Object, BackupType.Firmware, default))
            .ReturnsAsync(Result<DeviceBackup>.Success(new DeviceBackup { Id = Guid.NewGuid().ToString() }));

        _providerMock.Setup(p => p.UpgradeAsync(_deviceMock.Object, _image, default))
            .ReturnsAsync(Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult
            {
                Success = true
            }));

        _providerMock.SetupSequence(p => p.GetCurrentVersionAsync(_deviceMock.Object, default))
            .ReturnsAsync(Result<string>.Success("22.03.5"))
            .ReturnsAsync(Result<string>.Success("23.05.3"));

        // Act
        var result = await _service.UpgradeFirmwareAsync(_deviceMock.Object, _image);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.Equal("22.03.5", result.Value.PreviousVersion);
        Assert.Equal("23.05.3", result.Value.NewVersion);
    }
}
