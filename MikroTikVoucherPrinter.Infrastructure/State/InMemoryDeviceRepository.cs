using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.State;

namespace MikroTikVoucherPrinter.Infrastructure.State;

public class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly ConcurrentDictionary<Guid, DeviceState> _states = new();

    public Task<IEnumerable<DeviceState>> GetAllAsync()
    {
        return Task.FromResult(_states.Values.AsEnumerable());
    }

    public Task<DeviceState?> GetByIdAsync(Guid deviceId)
    {
        if (_states.TryGetValue(deviceId, out var state))
        {
            return Task.FromResult<DeviceState?>(state);
        }
        return Task.FromResult<DeviceState?>(null);
    }

    public Task UpdateAsync(DeviceState deviceState)
    {
        _states[deviceState.DeviceId] = deviceState;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid deviceId)
    {
        _states.TryRemove(deviceId, out _);
        return Task.CompletedTask;
    }
}
