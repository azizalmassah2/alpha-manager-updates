using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models.Monitoring;
using MikroTikVoucherPrinter.Application.Events;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Monitoring;

public class InMemoryDeviceMetricsStore : IDeviceMetricsStore
{
    // DeviceId -> Queue of Metrics
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<DeviceMetric>> _store = new();
    private const int MaxMetricsPerDevice = 1000;
    private readonly IEventBus _eventBus;

    public InMemoryDeviceMetricsStore(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<DeviceStateChangedEvent>(this, OnDeviceStateChanged);
    }

    private void OnDeviceStateChanged(DeviceStateChangedEvent ev)
    {
        var metric = new DeviceMetric
        {
            DeviceId = ev.DeviceState.DeviceId,
            Timestamp = DateTime.UtcNow,
            CpuUsage = ev.DeviceState.CpuUsage ?? 0,
            MemoryUsage = ev.DeviceState.MemoryUsage ?? 0,
            ActiveUsers = ev.DeviceState.ActiveUsers,
            Health = ev.DeviceState.Health
        };
        _ = StoreMetricAsync(metric);
    }

    public Task StoreMetricAsync(DeviceMetric metric)
    {
        var queue = _store.GetOrAdd(metric.DeviceId, _ => new ConcurrentQueue<DeviceMetric>());
        queue.Enqueue(metric);

        // Enforce limit
        while (queue.Count > MaxMetricsPerDevice)
        {
            queue.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<DeviceMetric>> GetMetricsAsync(Guid deviceId, DateTime since)
    {
        if (_store.TryGetValue(deviceId, out var queue))
        {
            return Task.FromResult(queue.Where(m => m.Timestamp >= since).ToList().AsEnumerable());
        }
        
        return Task.FromResult(Enumerable.Empty<DeviceMetric>());
    }

    public Task<IEnumerable<DeviceMetric>> GetLatestMetricsAsync(Guid deviceId, int count)
    {
        if (_store.TryGetValue(deviceId, out var queue))
        {
            return Task.FromResult(queue.Reverse().Take(count).Reverse().ToList().AsEnumerable());
        }

        return Task.FromResult(Enumerable.Empty<DeviceMetric>());
    }
}
