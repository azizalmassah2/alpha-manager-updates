using Lux.Management.Console.ViewModels;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models.Monitoring;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Events;

namespace Lux.Management.Console.Modules.Monitoring;

public partial class AlertsViewModel : ViewModelBase
{
    private readonly IAlertService _alertService;
    private readonly IDispatcherService _dispatcherService;

    [ObservableProperty]
    private ObservableCollection<Alert> _alerts = new();

    public AlertsViewModel(IPermissionService permissionService, IEventBus eventBus, IAlertService alertService, IDispatcherService dispatcherService) 
        : base(permissionService, eventBus)
    {
        _alertService = alertService;
        _dispatcherService = dispatcherService;
        _eventBus.Subscribe<AlertGeneratedEvent>(this, OnAlertGenerated);
        _ = LoadAlertsAsync();
    }

    private async Task LoadAlertsAsync()
    {
        var activeAlerts = await _alertService.GetActiveAlertsAsync();
        Alerts.Clear();
        foreach (var alert in activeAlerts)
        {
            Alerts.Add(alert);
        }
    }

    private void OnAlertGenerated(AlertGeneratedEvent ev)
    {
        _dispatcherService.Invoke(() =>
        {
            Alerts.Insert(0, ev.Alert);
        });
    }
}
