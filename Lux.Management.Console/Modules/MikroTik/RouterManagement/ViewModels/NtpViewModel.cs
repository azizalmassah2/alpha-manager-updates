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

public partial class NtpViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _primaryServer = string.Empty;

    [ObservableProperty]
    private string _secondaryServer = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public NtpViewModel(IActiveRouterContext activeRouterContext, IRouterManagementService routerService, IDialogService dialogService)
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
            IsEnabled = false;
            PrimaryServer = string.Empty;
            SecondaryServer = string.Empty;
            Status = string.Empty;
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var response = await _routerService.ExecuteQueryAsync("/system/ntp/client/print");
            var data = response.RawData.FirstOrDefault();

            if (data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsEnabled = data.GetValueOrDefault("enabled", "false") == "true" || data.GetValueOrDefault("enabled", "no") == "yes";
                    PrimaryServer = data.GetValueOrDefault("primary-ntp", data.GetValueOrDefault("servers", ""));
                    SecondaryServer = data.GetValueOrDefault("secondary-ntp", "");
                    Status = data.GetValueOrDefault("status", "unknown");
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load NTP settings: {ex.Message}";
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

        bool confirm = await _dialogService.ShowConfirmationAsync("هل أنت متأكد من حفظ إعدادات الوقت (NTP)؟");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "enabled", IsEnabled ? "yes" : "no" }
            };

            if (!string.IsNullOrWhiteSpace(PrimaryServer)) parameters.Add("primary-ntp", PrimaryServer);
            if (!string.IsNullOrWhiteSpace(SecondaryServer)) parameters.Add("secondary-ntp", SecondaryServer);

            await _routerService.ExecuteCommandAsync("/system/ntp/client/set", parameters);
            await _dialogService.ShowAlertAsync("تم حفظ إعدادات NTP بنجاح.", "نجاح");
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



