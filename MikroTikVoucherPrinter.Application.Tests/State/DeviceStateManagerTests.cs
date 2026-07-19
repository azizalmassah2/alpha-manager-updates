using System;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Events;
using MikroTikVoucherPrinter.Application.State;
using MikroTikVoucherPrinter.Infrastructure.State;
using Moq;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests.State;

public class DeviceStateManagerTests
{
    private readonly Mock<IDeviceRepository> _repositoryMock;
    private readonly Mock<IDeviceHealthEvaluator> _healthEvaluatorMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly DeviceStateManager _stateManager;

    public DeviceStateManagerTests()
    {
        _repositoryMock = new Mock<IDeviceRepository>();
        _healthEvaluatorMock = new Mock<IDeviceHealthEvaluator>();
        _eventBusMock = new Mock<IEventBus>();

        _stateManager = new DeviceStateManager(
            _repositoryMock.Object, 
            _healthEvaluatorMock.Object, 
            _eventBusMock.Object);
    }

    [Fact]
    public async Task UpdateTelemetryAsync_ShouldUpdateRepositoryAndPublishEvent()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(deviceId)).ReturnsAsync((DeviceState?)null);
        _healthEvaluatorMock.Setup(h => h.Evaluate(It.IsAny<DeviceState>())).Returns(DeviceHealthStatus.Healthy);

        // Act
        await _stateManager.UpdateTelemetryAsync(deviceId, 50.0, 60.0, 10);

        // Assert
        _repositoryMock.Verify(r => r.UpdateAsync(It.Is<DeviceState>(s => 
            s.DeviceId == deviceId && 
            s.CpuUsage == 50.0 && 
            s.MemoryUsage == 60.0 && 
            s.ActiveUsers == 10 && 
            s.IsOnline == true && 
            s.Health == DeviceHealthStatus.Healthy)), Times.Once);

        _eventBusMock.Verify(e => e.Publish(It.IsAny<DeviceStateChangedEvent>()), Times.Once);
    }

    [Fact]
    public async Task SetDeviceOfflineAsync_ShouldUpdateRepositoryAndPublishOfflineEvent()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var state = new DeviceState { DeviceId = deviceId, IsOnline = true };
        _repositoryMock.Setup(r => r.GetByIdAsync(deviceId)).ReturnsAsync(state);

        // Act
        await _stateManager.SetDeviceOfflineAsync(deviceId);

        // Assert
        _repositoryMock.Verify(r => r.UpdateAsync(It.Is<DeviceState>(s => 
            s.DeviceId == deviceId && 
            s.IsOnline == false && 
            s.Health == DeviceHealthStatus.Offline)), Times.Once);

        _eventBusMock.Verify(e => e.Publish(It.IsAny<DeviceOfflineEvent>()), Times.Once);
        _eventBusMock.Verify(e => e.Publish(It.IsAny<DeviceStateChangedEvent>()), Times.Once);
    }
}
