using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions.Models.Monitoring;
using MikroTikVoucherPrinter.Application.Events;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Monitoring;

public class InMemoryAlertService : IAlertService
{
    private readonly ConcurrentDictionary<Guid, Alert> _alerts = new();
    private readonly IEventBus _eventBus;
    private readonly List<AlertRule> _rules;

    public InMemoryAlertService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _rules = InitializeRules();
        _eventBus.Subscribe<DeviceStateChangedEvent>(this, OnDeviceStateChanged);
    }

    private void OnDeviceStateChanged(DeviceStateChangedEvent ev)
    {
        _ = EvaluateDeviceStateAsync(ev.DeviceState);
    }

    private List<AlertRule> InitializeRules()
    {
        return new List<AlertRule>
        {
            new() { Name = "CPU Critical", Description = "CPU Usage exceeds 95%", Severity = AlertSeverity.Critical, Condition = s => s.CpuUsage > 95 },
            new() { Name = "CPU Warning", Description = "CPU Usage exceeds 90%", Severity = AlertSeverity.Warning, Condition = s => s.CpuUsage > 90 && s.CpuUsage <= 95 },
            new() { Name = "Memory Critical", Description = "Memory Usage exceeds 95%", Severity = AlertSeverity.Critical, Condition = s => s.MemoryUsage > 95 },
            new() { Name = "Memory Warning", Description = "Memory Usage exceeds 90%", Severity = AlertSeverity.Warning, Condition = s => s.MemoryUsage > 90 && s.MemoryUsage <= 95 },
            new() { Name = "Device Offline", Description = "Device is offline", Severity = AlertSeverity.Critical, Condition = s => !s.IsOnline && (DateTime.UtcNow - s.LastSeen).TotalMinutes > 5 }
        };
    }

    public Task<IEnumerable<Alert>> GetActiveAlertsAsync()
    {
        return Task.FromResult(_alerts.Values.Where(a => !a.IsAcknowledged).OrderByDescending(a => a.Timestamp).AsEnumerable());
    }

    public Task<IEnumerable<Alert>> GetAlertsByDeviceAsync(Guid deviceId)
    {
        return Task.FromResult(_alerts.Values.Where(a => a.DeviceId == deviceId).OrderByDescending(a => a.Timestamp).AsEnumerable());
    }

    public Task AcknowledgeAlertAsync(Guid alertId)
    {
        if (_alerts.TryGetValue(alertId, out var alert))
        {
            alert.IsAcknowledged = true;
        }
        return Task.CompletedTask;
    }

    public async Task EvaluateDeviceStateAsync(DeviceState state)
    {
        foreach (var rule in _rules)
        {
            if (rule.Condition(state))
            {
                // Check if an unacknowledged alert for the same rule/device already exists to prevent spam
                var existingAlert = _alerts.Values.FirstOrDefault(a => 
                    a.DeviceId == state.DeviceId && 
                    !a.IsAcknowledged && 
                    a.Message == rule.Description);

                if (existingAlert == null)
                {
                    var alert = new Alert
                    {
                        DeviceId = state.DeviceId,
                        Severity = rule.Severity,
                        Message = rule.Description
                    };
                    await AddAlertAsync(alert);
                }
            }
        }
    }

    public Task AddAlertAsync(Alert alert)
    {
        _alerts.TryAdd(alert.Id, alert);
        _eventBus.Publish(new AlertGeneratedEvent(alert));
        return Task.CompletedTask;
    }
}
