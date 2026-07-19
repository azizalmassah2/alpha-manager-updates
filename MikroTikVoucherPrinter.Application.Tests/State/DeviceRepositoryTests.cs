using System;
using System.Linq;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Infrastructure.State;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests.State;

public class DeviceRepositoryTests
{
    [Fact]
    public async Task AddOrUpdate_ShouldStoreDevice()
    {
        // Arrange
        var repository = new InMemoryDeviceRepository();
        var deviceId = Guid.NewGuid();
        var state = new DeviceState { DeviceId = deviceId, DeviceName = "Test" };

        // Act
        await repository.UpdateAsync(state);
        var result = await repository.GetByIdAsync(deviceId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.DeviceName);
    }

    [Fact]
    public async Task Remove_ShouldDeleteDevice()
    {
        // Arrange
        var repository = new InMemoryDeviceRepository();
        var deviceId = Guid.NewGuid();
        var state = new DeviceState { DeviceId = deviceId };
        await repository.UpdateAsync(state);

        // Act
        await repository.RemoveAsync(deviceId);
        var result = await repository.GetByIdAsync(deviceId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAll_ShouldReturnAllDevices()
    {
        // Arrange
        var repository = new InMemoryDeviceRepository();
        await repository.UpdateAsync(new DeviceState { DeviceId = Guid.NewGuid() });
        await repository.UpdateAsync(new DeviceState { DeviceId = Guid.NewGuid() });

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Equal(2, result.Count());
    }
}
