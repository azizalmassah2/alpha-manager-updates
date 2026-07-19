using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public partial class RouterDashboardViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterHealthService _healthService;

    [ObservableProperty]
    private string _routerName = "لا يوجد اتصال";

    [ObservableProperty]
    private string _hostAddress = "-";

    [ObservableProperty]
    private RouterHealthStatus _healthStatus = new();

    public RouterDashboardViewModel(IActiveRouterContext activeRouterContext, IRouterHealthService healthService)
    {
        _activeRouterContext = activeRouterContext;
        _healthService = healthService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        _healthService.HealthUpdated += OnHealthUpdated;

        SyncState();
    }

    private void SyncState()
    {
        if (_activeRouterContext.IsConnected && _activeRouterContext.CurrentRouter != null)
        {
            RouterName = _activeRouterContext.CurrentRouter.DisplayName;
            HostAddress = _activeRouterContext.CurrentRouter.Host;
            HealthStatus = _healthService.CurrentStatus;
        }
        else
        {
            RouterName = "لا يوجد اتصال";
            HostAddress = "-";
            HealthStatus = new RouterHealthStatus();
        }
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(SyncState);
    }

    private void OnHealthUpdated(object? sender, RouterHealthStatus e)
    {
        Application.Current.Dispatcher.Invoke(() => 
        {
            HealthStatus = e;
        });
    }

    public void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        _healthService.HealthUpdated -= OnHealthUpdated;
        GC.SuppressFinalize(this);
    }
}
