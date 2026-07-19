using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public partial class DnsViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _servers = string.Empty;

    [ObservableProperty]
    private bool _allowRemoteRequests;

    [ObservableProperty]
    private string _cacheSize = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public DnsViewModel(IActiveRouterContext activeRouterContext, IRouterManagementService routerService, IDialogService dialogService)
    {
        _activeRouterContext = activeRouterContext;
        _routerService = routerService;
        _dialogService = dialogService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        
        var _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        await Task.Yield();
        if (!_activeRouterContext.IsConnected)
        {
            Servers = string.Empty;
            AllowRemoteRequests = false;
            CacheSize = string.Empty;
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var response = await _routerService.ExecuteQueryAsync("/ip/dns/print");
            var data = response.RawData.FirstOrDefault();

            if (data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Servers = data.GetValueOrDefault("servers", "");
                    AllowRemoteRequests = data.GetValueOrDefault("allow-remote-requests", "false") == "true";
                    CacheSize = data.GetValueOrDefault("cache-size", "");
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load DNS settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (!_activeRouterContext.IsConnected) return;

        bool confirm = await _dialogService.ShowConfirmationAsync("هل أنت متأكد من حفظ إعدادات DNS؟");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "servers", Servers },
                { "allow-remote-requests", AllowRemoteRequests ? "yes" : "no" }
            };

            await _routerService.ExecuteCommandAsync("/ip/dns/set", parameters);
            await _dialogService.ShowAlertAsync("تم حفظ إعدادات DNS بنجاح.", "نجاح");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل في حفظ الإعدادات: {ex.Message}", "خطأ");
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



