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
using System.IO;

using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.ViewModels;

namespace Lux.Management.Console.Modules.MikroTik.Backups.ViewModels;

public class BackupFileItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string CreationTime { get; set; } = string.Empty;
}

public partial class BackupsViewModel : ViewModelBase, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<BackupFileItem> _files = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public BackupsViewModel(
        IActiveRouterContext activeRouterContext, 
        IRouterManagementService routerService, 
        IDialogService dialogService,
        IPermissionService permissionService,
        IEventBus eventBus)
        : base(permissionService, eventBus)
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
        if (!_activeRouterContext.IsConnected)
        {
            Files.Clear();
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var response = await _routerService.ExecuteQueryAsync("/file/print");
            
            var items = response.RawData
                .Where(d => d.GetValueOrDefault("type", "").Contains("backup") || d.GetValueOrDefault("name", "").EndsWith(".rsc"))
                .Select(d => new BackupFileItem
                {
                    Id = d.GetValueOrDefault(".id", ""),
                    Name = d.GetValueOrDefault("name", ""),
                    Type = d.GetValueOrDefault("type", ""),
                    Size = FormatBytes(d.GetValueOrDefault("size", "0")),
                    CreationTime = d.GetValueOrDefault("creation-time", "")
                }).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Files.Clear();
                foreach (var item in items) Files.Add(item);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load backup files: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (!_activeRouterContext.IsConnected) return;
        
        string backupName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
        IsLoading = true;

        try
        {
            await _routerService.ExecuteCommandAsync("/system/backup/save", new Dictionary<string, string> { { "name", backupName } });
            await _dialogService.ShowAlertAsync($"تم إنشاء النسخة الاحتياطية ({backupName}.backup) بنجاح.", "نجاح");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل في إنشاء النسخة الاحتياطية: {ex.Message}", "خطأ");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateExportAsync()
    {
        if (!_activeRouterContext.IsConnected) return;

        string exportName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}";
        IsLoading = true;

        try
        {
            await _routerService.ExecuteCommandAsync("/export", new Dictionary<string, string> { { "file", exportName } });
            await _dialogService.ShowAlertAsync($"تم إنشاء ملف الإعدادات ({exportName}.rsc) بنجاح.", "نجاح");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل في تصدير الإعدادات: {ex.Message}", "خطأ");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteFileAsync(BackupFileItem? file)
    {
        if (file == null) return;

        bool confirm = await _dialogService.ShowConfirmationAsync($"هل أنت متأكد من حذف الملف {file.Name}؟");
        if (!confirm) return;

        try
        {
            await _routerService.ExecuteCommandAsync("/file/remove", new Dictionary<string, string> { { "numbers", file.Id } });
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل في حذف الملف: {ex.Message}", "خطأ");
        }
    }

    private string FormatBytes(string bytesString)
    {
        if (!long.TryParse(bytesString, out long bytes)) return bytesString;
        
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        if (bytes == 0) return "0 B";
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return $"{num} {suf[place]}";
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



