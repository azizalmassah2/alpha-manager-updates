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
using Lux.Management.Console.Core;
using System.Collections.Generic;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public class IpAddressItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
}

public partial class IpAddressesViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<IpAddressItem> _ipAddresses = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public IpAddressesViewModel(IActiveRouterContext activeRouterContext, IRouterManagementService routerService, IDialogService dialogService)
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
            IpAddresses.Clear();
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var response = await _routerService.ExecuteQueryAsync("/ip/address/print");
            var items = response.RawData.Select(d => new IpAddressItem
            {
                Id = d.GetValueOrDefault(".id", ""),
                Address = d.GetValueOrDefault("address", ""),
                Network = d.GetValueOrDefault("network", ""),
                Interface = d.GetValueOrDefault("interface", ""),
                IsDisabled = d.GetValueOrDefault("disabled", "false") == "true"
            }).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                IpAddresses.Clear();
                foreach (var item in items) IpAddresses.Add(item);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load IP addresses: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveAddressAsync(IpAddressItem? item)
    {
        if (item == null) return;

        bool confirm = await _dialogService.ShowConfirmationAsync($"هل أنت متأكد من حذف عنوان الـ IP ({item.Address})؟");
        if (!confirm) return;

        try
        {
            await _routerService.ExecuteCommandAsync("/ip/address/remove", new Dictionary<string, string> { { "numbers", item.Id } });
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل في حذف العنوان: {ex.Message}", "خطأ");
        }
    }

    // Add and Edit commands will require capturing user input. 
    // For now, we stub them to show an alert, as inline editing/dialogs might need specific Views.
    [RelayCommand]
    private async Task AddAddressAsync()
    {
        // Placeholder for adding IP Address form logic
        await _dialogService.ShowAlertAsync("سيتم إضافة نافذة تفاعلية لإدخال بيانات الـ IP في التحديث القادم.", "تحت الإنشاء");
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



