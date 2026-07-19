using System;
using System.Windows;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.Services;

public class NotificationService : INotificationService
{
    private readonly IEventBus _eventBus;

    public NotificationService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<MikroTikVoucherPrinter.Application.Events.DeviceStateChangedEvent>(this, OnDeviceStateChanged);
        _eventBus.Subscribe<MikroTikVoucherPrinter.Application.Events.FleetOperationCompletedEvent>(this, OnFleetOperationCompleted);
        _eventBus.Subscribe<MikroTikVoucherPrinter.Application.Events.AlertGeneratedEvent>(this, OnAlertGenerated);
    }

    private void OnDeviceStateChanged(MikroTikVoucherPrinter.Application.Events.DeviceStateChangedEvent e)
    {
        if (e.DeviceState.Health == Lux.Platform.Abstractions.Models.DeviceHealthStatus.Offline)
        {
            ShowWarning($"Device {e.DeviceState.DeviceName} is now Offline.", "Device Status");
        }
        else if (e.DeviceState.Health == Lux.Platform.Abstractions.Models.DeviceHealthStatus.Healthy)
        {
            ShowInfo($"Device {e.DeviceState.DeviceName} is Healthy.", "Device Status");
        }
    }

    private void OnFleetOperationCompleted(MikroTikVoucherPrinter.Application.Events.FleetOperationCompletedEvent e)
    {
        if (e.Operation.Status == Lux.Platform.Abstractions.Models.FleetOperationStatus.Completed)
            ShowSuccess($"Operation {e.Operation.Type} completed successfully.", "Fleet Operation");
        else if (e.Operation.Status == Lux.Platform.Abstractions.Models.FleetOperationStatus.Failed)
            ShowError($"Operation {e.Operation.Type} failed.", "Fleet Operation");
    }

    private void OnAlertGenerated(MikroTikVoucherPrinter.Application.Events.AlertGeneratedEvent e)
    {
        if (e.Alert.Severity == Lux.Platform.Abstractions.Models.Monitoring.AlertSeverity.Critical)
            ShowError(e.Alert.Message, "Critical Alert");
        else if (e.Alert.Severity == Lux.Platform.Abstractions.Models.Monitoring.AlertSeverity.Warning)
            ShowWarning(e.Alert.Message, "Warning");
    }

    public void ShowSuccess(string message, string? title = null)
    {
        _eventBus.Publish(new NotificationEvent(message, title, "Success"));
    }

    public void ShowInfo(string message, string? title = null)
    {
        _eventBus.Publish(new NotificationEvent(message, title, "Info"));
    }

    public void ShowWarning(string message, string? title = null)
    {
        _eventBus.Publish(new NotificationEvent(message, title, "Warning"));
    }

    public void ShowError(string message, string? title = null)
    {
        _eventBus.Publish(new NotificationEvent(message, title, "Error"));
    }
}

public record NotificationEvent(string Message, string? Title, string Type);
