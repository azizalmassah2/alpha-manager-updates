using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Services;
using MikroTikVoucherPrinter.Domain.Entities;
using Moq;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class UnifiedConfigurationServiceTests
{
    private readonly Mock<IDeviceConfigurationProvider> _providerMock;
    private readonly Mock<IUnifiedBackupService> _backupServiceMock;
    private readonly UnifiedConfigurationService _service;
    private readonly NetworkDevice _device;

    public UnifiedConfigurationServiceTests()
    {
        _providerMock = new Mock<IDeviceConfigurationProvider>();
        _backupServiceMock = new Mock<IUnifiedBackupService>();

        _service = new UnifiedConfigurationService(
            new List<IDeviceConfigurationProvider> { _providerMock.Object },
            _backupServiceMock.Object
        );

        _device = new NetworkDevice { Vendor = DeviceVendor.OpenWrt };
        _providerMock.Setup(p => p.CanHandle(It.IsAny<IDevice>())).Returns(true);
    }

    [Fact]
    public async Task ApplyConfigurationAsync_ValidationFails_ReturnsFailure()
    {
        // Arrange
        var config = new DeviceConfiguration { Sections = new List<ConfigurationSection> { new ConfigurationSection() } };
        _providerMock.Setup(p => p.ValidateConfigurationAsync(config, default))
            .ReturnsAsync(Result<ConfigurationValidationResult>.Success(ConfigurationValidationResult.Failure("Invalid IP")));

        // Act
        var result = await _service.ApplyConfigurationAsync(_device, config);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Validation failed", result.ErrorMessage);
        
        // Ensure Backup was NOT called
        _backupServiceMock.Verify(b => b.CreateBackupAsync(It.IsAny<IDevice>(), It.IsAny<BackupType>(), default), Times.Never);
        // Ensure Apply was NOT called
        _providerMock.Verify(p => p.ApplyConfigurationAsync(It.IsAny<IDevice>(), It.IsAny<DeviceConfiguration>(), default), Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationAsync_ApplyFails_TriggersRollback()
    {
        // Arrange
        var config = new DeviceConfiguration { Sections = new List<ConfigurationSection> { new ConfigurationSection() } };
        var backup = new DeviceBackup();
        
        _providerMock.Setup(p => p.ValidateConfigurationAsync(config, default))
            .ReturnsAsync(Result<ConfigurationValidationResult>.Success(ConfigurationValidationResult.Success()));

        _backupServiceMock.Setup(b => b.CreateBackupAsync(_device, BackupType.PreDeploymentRollback, default))
            .ReturnsAsync(Result<DeviceBackup>.Success(backup));

        _providerMock.Setup(p => p.ApplyConfigurationAsync(_device, config, default))
            .ReturnsAsync(Result.Failure("Apply Failed", ErrorType.ExternalService));

        _backupServiceMock.Setup(b => b.RestoreBackupAsync(_device, backup, default))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _service.ApplyConfigurationAsync(_device, config);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Rollback successful", result.ErrorMessage);
        
        // Ensure restore was called
        _backupServiceMock.Verify(b => b.RestoreBackupAsync(_device, backup, default), Times.Once);
    }
    
    [Fact]
    public async Task ApplyConfigurationAsync_Success_DoesNotTriggerRollback()
    {
        // Arrange
        var config = new DeviceConfiguration { Sections = new List<ConfigurationSection> { new ConfigurationSection() } };
        var backup = new DeviceBackup();
        
        _providerMock.Setup(p => p.ValidateConfigurationAsync(config, default))
            .ReturnsAsync(Result<ConfigurationValidationResult>.Success(ConfigurationValidationResult.Success()));

        _backupServiceMock.Setup(b => b.CreateBackupAsync(_device, BackupType.PreDeploymentRollback, default))
            .ReturnsAsync(Result<DeviceBackup>.Success(backup));

        _providerMock.Setup(p => p.ApplyConfigurationAsync(_device, config, default))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _service.ApplyConfigurationAsync(_device, config);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Ensure restore was NEVER called
        _backupServiceMock.Verify(b => b.RestoreBackupAsync(It.IsAny<IDevice>(), It.IsAny<DeviceBackup>(), default), Times.Never);
    }
}
