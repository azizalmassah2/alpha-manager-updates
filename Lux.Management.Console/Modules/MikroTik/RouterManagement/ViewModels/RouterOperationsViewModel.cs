using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public partial class RouterOperationsViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _isConnected;

    public RouterOperationsViewModel(IActiveRouterContext activeRouterContext, IRouterManagementService routerService, IDialogService dialogService)
    {
        _activeRouterContext = activeRouterContext;
        _routerService = routerService;
        _dialogService = dialogService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        SyncState();
    }

    private void SyncState()
    {
        IsConnected = _activeRouterContext.IsConnected;
    }

    [RelayCommand]
    private async Task RebootAsync()
    {
        if (!_activeRouterContext.IsConnected) return;

        bool confirm = await _dialogService.ShowConfirmationAsync("هل أنت متأكد من إعادة تشغيل الروتر (Reboot)؟", "تحذير خطير");
        if (!confirm) return;

        try
        {
            await _routerService.ExecuteCommandAsync("/system/reboot");
            await _dialogService.ShowAlertAsync("تم إرسال أمر إعادة التشغيل بنجاح. سيتم قطع الاتصال قريباً.", "نجاح");
            // App will auto-disconnect when the socket drops.
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل في إعادة التشغيل: {ex.Message}", "خطأ");
        }
    }

    [RelayCommand]
    private async Task ShutdownAsync()
    {
        if (!_activeRouterContext.IsConnected) return;

        bool confirm = await _dialogService.ShowConfirmationAsync("هل أنت متأكد من إيقاف تشغيل الروتر (Shutdown)؟ ستحتاج إلى إعادة تشغيله يدوياً.", "تحذير خطير جداً");
        if (!confirm) return;

        try
        {
            await _routerService.ExecuteCommandAsync("/system/shutdown");
            await _dialogService.ShowAlertAsync("تم إرسال أمر إيقاف التشغيل بنجاح. سيتم قطع الاتصال قريباً.", "نجاح");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل في إيقاف التشغيل: {ex.Message}", "خطأ");
        }
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(SyncState);
    }

    public void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        GC.SuppressFinalize(this);
    }
}
