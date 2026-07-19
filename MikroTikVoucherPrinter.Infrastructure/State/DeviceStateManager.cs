using System;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Events;
using MikroTikVoucherPrinter.Application.State;
using Lux.Platform.Abstractions.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.State;

public class DeviceStateManager : IDeviceStateManager
{
    private readonly IDeviceRepository _repository;
    private readonly IDeviceHealthEvaluator _healthEvaluator;
    private readonly IEventBus _eventBus;

    public DeviceStateManager(IDeviceRepository repository, IDeviceHealthEvaluator healthEvaluator, IEventBus eventBus)
    {
        _repository = repository;
        _healthEvaluator = healthEvaluator;
        _eventBus = eventBus;
    }

    public async Task UpdateTelemetryAsync(Guid deviceId, double? cpuUsage, double? memoryUsage, int activeUsers)
    {
        var state = await _repository.GetByIdAsync(deviceId) ?? new DeviceState { DeviceId = deviceId };
        
        var oldHealth = state.Health;
        
        state.CpuUsage = cpuUsage;
        state.MemoryUsage = memoryUsage;
        state.ActiveUsers = activeUsers;
        state.IsOnline = true;
        state.LastSeen = DateTime.UtcNow;
        
        state.Health = _healthEvaluator.Evaluate(state);
        
        await _repository.UpdateAsync(state);
        _eventBus.Publish(new DeviceStateChangedEvent(state));
        
        if (oldHealth != state.Health)
        {
            _eventBus.Publish(new DeviceHealthChangedEvent(deviceId, oldHealth, state.Health));
        }
    }

    public async Task UpdateFirmwareAsync(Guid deviceId, string version)
    {
        var state = await _repository.GetByIdAsync(deviceId) ?? new DeviceState { DeviceId = deviceId };
        state.FirmwareVersion = version;
        state.LastSeen = DateTime.UtcNow;
        await _repository.UpdateAsync(state);
        _eventBus.Publish(new DeviceStateChangedEvent(state));
    }

    public async Task UpdateConfigurationAsync(Guid deviceId, string status)
    {
        // Currently DeviceState doesn't store Configuration Status specifically, but we could add it later.
        // For now, we just update LastSeen.
        var state = await _repository.GetByIdAsync(deviceId) ?? new DeviceState { DeviceId = deviceId };
        state.LastSeen = DateTime.UtcNow;
        await _repository.UpdateAsync(state);
        _eventBus.Publish(new DeviceStateChangedEvent(state));
    }

    public async Task UpdateOperationResultAsync(Guid deviceId, bool success)
    {
        var state = await _repository.GetByIdAsync(deviceId) ?? new DeviceState { DeviceId = deviceId };
        state.LastSeen = DateTime.UtcNow;
        await _repository.UpdateAsync(state);
        _eventBus.Publish(new DeviceStateChangedEvent(state));
    }

    public async Task SetDeviceOnlineAsync(Guid deviceId)
    {
        var state = await _repository.GetByIdAsync(deviceId) ?? new DeviceState { DeviceId = deviceId };
        if (!state.IsOnline)
        {
            state.IsOnline = true;
            state.LastSeen = DateTime.UtcNow;
            state.Health = _healthEvaluator.Evaluate(state);
            await _repository.UpdateAsync(state);
            _eventBus.Publish(new DeviceOnlineEvent(deviceId));
            _eventBus.Publish(new DeviceStateChangedEvent(state));
        }
    }

    public async Task SetDeviceOfflineAsync(Guid deviceId)
    {
        var state = await _repository.GetByIdAsync(deviceId) ?? new DeviceState { DeviceId = deviceId };
        if (state.IsOnline)
        {
            state.IsOnline = false;
            state.Health = DeviceHealthStatus.Offline;
            await _repository.UpdateAsync(state);
            _eventBus.Publish(new DeviceOfflineEvent(deviceId));
            _eventBus.Publish(new DeviceStateChangedEvent(state));
        }
    }

    public async Task RefreshAllDevicesAsync()
    {
        // This will be called by AutoRefreshService.
        // It should coordinate with DeviceMonitor to pull latest data and then call UpdateTelemetryAsync.
        // Since we don't inject IDeviceMonitor here to avoid circular dependencies if IDeviceMonitor uses IDeviceStateManager,
        // we can just let AutoRefreshService orchestrate that. So maybe RefreshAllDevicesAsync isn't needed here, 
        // or it's implemented differently. For now, it's an empty shell, or we can remove it from interface.
        // Let's implement it to just publish a global refresh event, or we let AutoRefreshService handle the coordination.
        await Task.CompletedTask;
    }
}
