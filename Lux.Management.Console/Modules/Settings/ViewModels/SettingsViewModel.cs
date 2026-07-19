using Lux.Management.Console.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace Lux.Management.Console.Modules.Settings.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    public SettingsViewModel(
        IPermissionService permissionService, 
        IEventBus eventBus,
        ISettingsService settingsService) : base(permissionService, eventBus)
    {
        _settingsService = settingsService;
        _autoConnectOnStartup = _settingsService.Get("AutoConnectOnStartup", true);
        _nocMonitoringInterval = _settingsService.Get("NocMonitoringInterval", 100);
        _nocPingTimeout = _settingsService.Get("NocPingTimeout", 2000);
        _userManagerImportInterval = _settingsService.Get("UserManagerImportInterval", 5);
    }

    [ObservableProperty]
    private bool _autoConnectOnStartup;

    [ObservableProperty]
    private int _nocMonitoringInterval;

    [ObservableProperty]
    private int _nocPingTimeout;

    [ObservableProperty]
    private int _userManagerImportInterval;

    partial void OnAutoConnectOnStartupChanged(bool value)
    {
        _settingsService.Set("AutoConnectOnStartup", value);
        _ = _settingsService.SaveAsync();
    }

    partial void OnNocMonitoringIntervalChanged(int value)
    {
        if (value < 1) value = 1;
        _settingsService.Set("NocMonitoringInterval", value);
        _ = _settingsService.SaveAsync();
    }

    partial void OnNocPingTimeoutChanged(int value)
    {
        if (value < 100) value = 100;
        _settingsService.Set("NocPingTimeout", value);
        _ = _settingsService.SaveAsync();
    }

    partial void OnUserManagerImportIntervalChanged(int value)
    {
        if (value < 1) value = 1;
        _settingsService.Set("UserManagerImportInterval", value);
        _ = _settingsService.SaveAsync();
    }
}
