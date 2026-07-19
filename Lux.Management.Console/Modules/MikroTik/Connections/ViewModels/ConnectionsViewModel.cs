using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Modules.MikroTik.Connections.Services;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Modules.MikroTik.Connections.ViewModels;

public partial class ConnectionsViewModel : ViewModelBase
{
    private readonly IRouterRepository _routerRepository;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IConnectionTestService _connectionTestService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IRouterDialogService _routerDialogService;

    [ObservableProperty]
    private ObservableCollection<Router> _routers = new();

    [ObservableProperty]
    private Router? _selectedRouter;

    public ConnectionsViewModel(
        IPermissionService permissionService, 
        IEventBus eventBus,
        IRouterRepository routerRepository,
        IActiveRouterContext activeRouterContext,
        IConnectionTestService connectionTestService,
        IDialogService dialogService,
        INotificationService notificationService,
        IRouterDialogService routerDialogService) 
        : base(permissionService, eventBus)
    {
        _routerRepository = routerRepository;
        _activeRouterContext = activeRouterContext;
        _connectionTestService = connectionTestService;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _routerDialogService = routerDialogService;
    }

    [RelayCommand]
    private async Task LoadRoutersAsync()
    {
        try
        {
            var list = await _routerRepository.GetAllAsync();
            Routers.Clear();
            foreach (var r in list)
            {
                Routers.Add(r);
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"فشل في تحميل الروترات: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AddRouterAsync()
    {
        var result = await _routerDialogService.ShowAddEditRouterDialogAsync();
        if (result != null)
        {
            try
            {
                await _routerRepository.AddAsync(result);
                _notificationService.ShowSuccess($"تم إضافة الروتر {result.DisplayName} بنجاح");
                await LoadRoutersAsync();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"خطأ أثناء الإضافة: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task EditRouterAsync(Router router)
    {
        if (router == null) return;

        var result = await _routerDialogService.ShowAddEditRouterDialogAsync(router);
        if (result != null)
        {
            try
            {
                await _routerRepository.UpdateAsync(result);
                _notificationService.ShowSuccess($"تم تعديل الروتر {result.DisplayName} بنجاح");
                await LoadRoutersAsync();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"خطأ أثناء التعديل: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync(Router router)
    {
        if (router == null) return;
        
        var result = await _connectionTestService.TestConnectionAsync(router);
        if (result.Success)
        {
            _notificationService.ShowSuccess(result.Reason ?? "تم الاتصال بنجاح");
        }
        else
        {
            _notificationService.ShowError(result.Reason ?? "فشل الاتصال");
        }
    }

    [RelayCommand]
    private async Task ConnectRouterAsync(Router router)
    {
        if (router == null) return;

        try
        {
            await _activeRouterContext.SwitchRouterAsync(router);
            _notificationService.ShowSuccess($"تم الاتصال بالروتر: {router.DisplayName}");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"فشل الاتصال: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteRouterAsync(Router router)
    {
        if (router == null) return;

        if (_activeRouterContext.CurrentRouter?.Id == router.Id)
        {
            await _dialogService.ShowAlertAsync("لا يمكن حذف الروتر وهو متصل حالياً. يرجى قطع الاتصال أو التبديل إلى روتر آخر قبل الحذف.");
            return;
        }

        bool confirm = await _dialogService.ShowConfirmationAsync($"هل أنت متأكد من حذف الروتر '{router.DisplayName}'؟");
        if (confirm)
        {
            try
            {
                await _routerRepository.DeleteAsync(router.Id);
                Routers.Remove(router);
                _notificationService.ShowSuccess("تم حذف الروتر بنجاح");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"فشل في حذف الروتر: {ex.Message}");
            }
        }
    }
}
