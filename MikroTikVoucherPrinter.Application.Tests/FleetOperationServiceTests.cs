using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Services;
using MikroTikVoucherPrinter.Domain.Entities;
using Moq;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class FleetOperationServiceTests
{
    private readonly Mock<IProvisioningOrchestrator> _provisioningMock;
    private readonly Mock<IUnifiedBackupService> _backupMock;
    private readonly InMemoryOperationHistoryRepository _historyRepo;
    private readonly FleetOperationService _service;

    public FleetOperationServiceTests()
    {
        _provisioningMock = new Mock<IProvisioningOrchestrator>();
        _backupMock = new Mock<IUnifiedBackupService>();
        _historyRepo = new InMemoryOperationHistoryRepository();

        var firmwareMock = new Mock<IUnifiedFirmwareService>();

        _service = new FleetOperationService(
            _provisioningMock.Object,
            _backupMock.Object,
            firmwareMock.Object,
            _historyRepo,
            new Mock<IEventBus>().Object,
            new NullLogger<FleetOperationService>()
        );
    }

    [Fact]
    public async Task StartProvisioningAsync_ExecutesAndCompletes()
    {
        // Arrange
        var d1 = new NetworkDevice { Name = "D1" };
        var d2 = new NetworkDevice { Name = "D2" };
        var devices = new List<IDevice> { d1, d2 };
        var template = new ProvisioningTemplate();

        _provisioningMock.Setup(p => p.ProvisionDeviceAsync(It.IsAny<IDevice>(), template, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DeviceProvisioningResult>.Success(new DeviceProvisioningResult { IsSuccess = true }));

        // Act
        var opId = await _service.StartProvisioningAsync(devices, template);

        // Wait a bit for background execution to complete
        await Task.Delay(500);

        // Assert
        var op = await _historyRepo.GetByIdAsync(opId);
        Assert.NotNull(op);
        Assert.Equal(FleetOperationStatus.Completed, op.Status);
        Assert.Equal(2, op.Progress.TotalDevices);
        Assert.Equal(2, op.Progress.ProcessedDevices);
        Assert.Equal(2, op.Progress.SuccessfulDevices);
        Assert.Equal(0, op.Progress.FailedDevices);
    }
}
