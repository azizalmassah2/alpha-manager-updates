using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public partial class SystemResourcesViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterHealthService _healthService;

    [ObservableProperty]
    private RouterHealthStatus _resources = new();

    [ObservableProperty]
    private bool _isConnected;

    public SystemResourcesViewModel(IActiveRouterContext activeRouterContext, IRouterHealthService healthService)
    {
        _activeRouterContext = activeRouterContext;
        _healthService = healthService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        _healthService.HealthUpdated += OnHealthUpdated;

        SyncState();
    }

    private void SyncState()
    {
        IsConnected = _activeRouterContext.IsConnected;
        if (IsConnected)
        {
            Resources = _healthService.CurrentStatus;
        }
        else
        {
            Resources = new RouterHealthStatus();
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
            Resources = e;
        });
    }

    public void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        _healthService.HealthUpdated -= OnHealthUpdated;
        GC.SuppressFinalize(this);
    }
}
