using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.Modules.Home.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly INavigationService _navigationService;
    private readonly IDispatcherService _dispatcherService;

    [ObservableProperty]
    private string _activeRouterName = "لا يوجد راوتر نشط";

    [ObservableProperty]
    private string _activeRouterHost = "-";

    [ObservableProperty]
    private string _connectionStatus = "غير متصل";

    [ObservableProperty]
    private bool _isConnected;

    public HomeViewModel(
        IPermissionService permissionService,
        IEventBus eventBus,
        IActiveRouterContext activeRouterContext,
        INavigationService navigationService,
        IDispatcherService dispatcherService) 
        : base(permissionService, eventBus)
    {
        _activeRouterContext = activeRouterContext;
        _navigationService = navigationService;
        _dispatcherService = dispatcherService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        UpdateActiveRouterInfo();
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        _dispatcherService.InvokeAsync(UpdateActiveRouterInfo);
    }

    private void UpdateActiveRouterInfo()
    {
        IsConnected = _activeRouterContext.IsConnected;
        if (_activeRouterContext.CurrentRouter != null)
        {
            ActiveRouterName = _activeRouterContext.CurrentRouter.DisplayName;
            ActiveRouterHost = _activeRouterContext.CurrentRouter.Host;
            ConnectionStatus = IsConnected ? "متصل" : "جاري الاتصال";
        }
        else
        {
            ActiveRouterName = "لا يوجد راوتر نشط";
            ActiveRouterHost = "-";
            ConnectionStatus = "غير متصل";
        }
    }

    public override void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        base.Dispose();
    }
}
