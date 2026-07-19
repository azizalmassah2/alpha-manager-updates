using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;
using Lux.Platform.Abstractions.Common;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public class InterfaceItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public bool IsDisabled { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public string Mtu { get; set; } = string.Empty;
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
}

public partial class InterfacesViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;

    [ObservableProperty]
    private ObservableCollection<InterfaceItem> _interfaces = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public InterfacesViewModel(IActiveRouterContext activeRouterContext, IRouterManagementService routerService)
    {
        _activeRouterContext = activeRouterContext;
        _routerService = routerService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        
        var _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        await Task.Yield();
        if (!_activeRouterContext.IsConnected)
        {
            Interfaces.Clear();
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var response = await _routerService.ExecuteQueryAsync("/interface/print");
            var items = response.RawData.Select(d => new InterfaceItem
            {
                Id = d.GetValueOrDefault(".id", ""),
                Name = d.GetValueOrDefault("name", ""),
                Type = d.GetValueOrDefault("type", ""),
                IsRunning = d.GetValueOrDefault("running", "false") == "true",
                IsDisabled = d.GetValueOrDefault("disabled", "false") == "true",
                MacAddress = d.GetValueOrDefault("mac-address", ""),
                Mtu = d.GetValueOrDefault("mtu", ""),
                RxBytes = long.TryParse(d.GetValueOrDefault("rx-byte", "0"), out var rx) ? rx : 0,
                TxBytes = long.TryParse(d.GetValueOrDefault("tx-byte", "0"), out var tx) ? tx : 0
            }).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Interfaces.Clear();
                foreach (var item in items) Interfaces.Add(item);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load interfaces: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() => 
        {
            var _ = LoadDataAsync();
        });
    }

    public void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        GC.SuppressFinalize(this);
    }
}


