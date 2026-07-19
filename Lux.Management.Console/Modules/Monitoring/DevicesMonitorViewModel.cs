using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces.Telemetry;
using MikroTikVoucherPrinter.Domain.Models.Telemetry;

namespace Lux.Management.Console.Modules.Monitoring;

public partial class DevicesMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly IMonitoringEngine _monitoringEngine;
    private readonly IUserNotificationService _notificationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IAutoRefreshService _autoRefreshService;

    [ObservableProperty]
    private ObservableCollection<PollingSession> _sessions = new();

    public DevicesMonitorViewModel(
        IPermissionService permissionService,
        IEventBus eventBus,
        IMonitoringEngine monitoringEngine,
        IUserNotificationService notificationService,
        IDispatcherService dispatcherService,
        IAutoRefreshService autoRefreshService) 
        : base(permissionService, eventBus)
    {
        _monitoringEngine = monitoringEngine;
        _notificationService = notificationService;
        _dispatcherService = dispatcherService;
        _autoRefreshService = autoRefreshService;

        _autoRefreshService.RegisterCallback(LoadSessionsAsync);

        _ = LoadSessionsAsync();
    }

    private Task LoadSessionsAsync()
    {
        try
        {
            var sessions = _monitoringEngine.GetMonitoringStatus().OrderBy(s => s.DeviceId);
            
            _dispatcherService.Invoke(() =>
            {
                // Simple refresh for now. For better performance with many devices, 
                // we would do smart updates instead of clear/add.
                Sessions.Clear();
                foreach (var session in sessions)
                {
                    Sessions.Add(session);
                }
            });
        }
        catch (Exception)
        {
            // Silent catch to prevent UI timer crashes
        }
        
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task StopMonitoringAsync(PollingSession session)
    {
        if (session == null) return;
        try
        {
            await _monitoringEngine.StopMonitoringDeviceAsync(session.DeviceId);
            _notificationService.ShowSuccess("Monitoring stopped for device.");
            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"Failed to stop monitoring: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _autoRefreshService.UnregisterCallback(LoadSessionsAsync);
    }
}


