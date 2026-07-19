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

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public class RouteItem
{
    public string Id { get; set; } = string.Empty;
    public string DstAddress { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDynamic { get; set; }
    public bool IsStatic { get; set; }
}

public partial class RoutesViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;

    [ObservableProperty]
    private ObservableCollection<RouteItem> _routes = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public RoutesViewModel(IActiveRouterContext activeRouterContext, IRouterManagementService routerService)
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
            Routes.Clear();
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var response = await _routerService.ExecuteQueryAsync("/ip/route/print");
            var items = response.RawData.Select(d => new RouteItem
            {
                Id = d.GetValueOrDefault(".id", ""),
                DstAddress = d.GetValueOrDefault("dst-address", ""),
                Gateway = d.GetValueOrDefault("gateway", ""),
                Distance = d.GetValueOrDefault("distance", ""),
                IsActive = d.GetValueOrDefault("active", "false") == "true",
                IsDynamic = d.GetValueOrDefault("dynamic", "false") == "true",
                IsStatic = d.GetValueOrDefault("static", "false") == "true"
            }).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Routes.Clear();
                foreach (var item in items) Routes.Add(item);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load routes: {ex.Message}";
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


