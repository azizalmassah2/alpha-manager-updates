using Lux.Management.Console.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core;
using MikroTikVoucherPrinter.Domain.Interfaces;
using System.IO;
using System.Linq;
using System;

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
        _backupPath = _settingsService.Get("BackupPath", GetDefaultBackupPath());
    }

    [ObservableProperty]
    private bool _autoConnectOnStartup;

    [ObservableProperty]
    private int _nocMonitoringInterval;

    [ObservableProperty]
    private int _nocPingTimeout;

    [ObservableProperty]
    private int _userManagerImportInterval;

    [ObservableProperty]
    private string _backupPath = string.Empty;

    private string GetDefaultBackupPath()
    {
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            var nonSystemDrive = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.DriveType == DriveType.Fixed && !string.Equals(d.Name, systemDrive, StringComparison.OrdinalIgnoreCase));
            
            if (nonSystemDrive != null)
            {
                return Path.Combine(nonSystemDrive.Name, "AlphaManagerBackups");
            }
        }
        catch { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AlphaManagerBackups");
    }

    [RelayCommand]
    private void ChangeBackupPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "اختر مجلد حفظ النسخ الاحتياطية",
            InitialDirectory = Directory.Exists(BackupPath) ? BackupPath : ""
        };
        if (dialog.ShowDialog() == true)
        {
            BackupPath = dialog.FolderName;
        }
    }

    partial void OnBackupPathChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        _settingsService.Set("BackupPath", value);
        _ = _settingsService.SaveAsync();
    }

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
