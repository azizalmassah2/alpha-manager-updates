using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class FirmwareUpgradeWorkflowTests
{
    private readonly Mock<IProvisioningOrchestrator> _provisioningMock;
    private readonly Mock<IUnifiedBackupService> _backupServiceMock;
    private readonly Mock<IUnifiedFirmwareService> _firmwareServiceMock;
    private readonly Mock<IOperationHistoryRepository> _repositoryMock;
    private readonly FleetOperationService _service;
    private readonly Mock<IDevice> _deviceMock;
    private readonly FirmwareImage _image;

    public FirmwareUpgradeWorkflowTests()
    {
        _provisioningMock = new Mock<IProvisioningOrchestrator>();
        _backupServiceMock = new Mock<IUnifiedBackupService>();
        _firmwareServiceMock = new Mock<IUnifiedFirmwareService>();
        _repositoryMock = new Mock<IOperationHistoryRepository>();

        _deviceMock = new Mock<IDevice>();
        _deviceMock.Setup(d => d.Id).Returns("dev-1");
        _deviceMock.Setup(d => d.Name).Returns("Router1");

        _service = new FleetOperationService(
            _provisioningMock.Object,
            _backupServiceMock.Object,
            _firmwareServiceMock.Object,
            _repositoryMock.Object,
            new Mock<IEventBus>().Object,
            NullLogger<FleetOperationService>.Instance);

        _image = new FirmwareImage { Name = "test.bin", Version = "1.0.0" };
    }

    [Fact]
    public async Task StartFirmwareUpgradeAsync_ExecutesAndSavesOperation()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SaveAsync(It.IsAny<FleetOperation>(), default)).Returns(Task.CompletedTask);

        _firmwareServiceMock.Setup(f => f.UpgradeFirmwareAsync(_deviceMock.Object, _image, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult { Success = true }));

        // Act
        var operationId = await _service.StartFirmwareUpgradeAsync(new[] { _deviceMock.Object }, _image);

        // Wait briefly for background task to complete
        await Task.Delay(100);

        // Assert
        Assert.NotEqual(Guid.Empty, operationId);
        
        // Verify operation saved initially
        _repositoryMock.Verify(r => r.SaveAsync(It.Is<FleetOperation>(o => o.Type == FleetOperationType.FirmwareUpgrade), default), Times.AtLeastOnce);
        
        // Verify firmware service called
        _firmwareServiceMock.Verify(f => f.UpgradeFirmwareAsync(_deviceMock.Object, _image, It.IsAny<CancellationToken>()), Times.Once);
    }
}
