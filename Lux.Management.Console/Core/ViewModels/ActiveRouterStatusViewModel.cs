using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikroTikVoucherPrinter.Domain.Enums.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.Core.ViewModels;

public partial class ActiveRouterStatusViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;

    [ObservableProperty]
    private string _activeRouterName = "غير متصل";

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _routerOSVersion = string.Empty;

    [ObservableProperty]
    private ConnectionState _state = ConnectionState.Disconnected;

    [ObservableProperty]
    private bool _isConnected;

    public ActiveRouterStatusViewModel(IActiveRouterContext activeRouterContext)
    {
        _activeRouterContext = activeRouterContext;
        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        
        UpdateStatus();
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        // Must marshal to UI thread using App.Current.Dispatcher, but here we can just update observable properties.
        // If not running on UI thread, CommunityToolkit handles INotifyPropertyChanged marshalling automatically if configured,
        // but it's safer to ensure we update.
        System.Windows.Application.Current.Dispatcher.Invoke(UpdateStatus);
    }

    private void UpdateStatus()
    {
        State = _activeRouterContext.State;
        IsConnected = _activeRouterContext.IsConnected;

        if (_activeRouterContext.CurrentRouter != null && _activeRouterContext.IsConnected)
        {
            ActiveRouterName = _activeRouterContext.CurrentRouter.DisplayName;
            Host = _activeRouterContext.CurrentRouter.Host;
            RouterOSVersion = _activeRouterContext.CurrentRouter.RouterOSVersion ?? string.Empty;
        }
        else
        {
            ActiveRouterName = "غير متصل";
            Host = string.Empty;
            RouterOSVersion = string.Empty;
        }
    }

    public void Dispose()
    {
        if (_activeRouterContext != null)
        {
            _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _activeRouterContext.DisconnectAsync();
    }
}
