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
    private readonly ISecureStorageService _secureStorageService;

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
        IEventBus eventBus,
        ISecureStorageService secureStorageService)
        : base(permissionService, eventBus)
    {
        _activeRouterContext = activeRouterContext;
        _routerService = routerService;
        _dialogService = dialogService;
        _secureStorageService = secureStorageService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        
        var _ = LoadDataAsync();
    }

    private async Task<bool> DownloadFileViaFtpAsync(string remoteFileName, string localFilePath)
    {
        try
        {
            var router = _activeRouterContext.CurrentRouter;
            if (router == null) return false;
            
            string password = "";
            if (!string.IsNullOrEmpty(router.EncryptedPassword))
            {
                password = _secureStorageService.Decrypt(router.EncryptedPassword);
            }

            var ftpUrl = $"ftp://{router.Host}/{remoteFileName}";
            var req = (System.Net.FtpWebRequest)System.Net.WebRequest.Create(ftpUrl);
            req.Method = System.Net.WebRequestMethods.Ftp.DownloadFile;
            req.Credentials = new System.Net.NetworkCredential(router.Username, password);
            req.UsePassive = true;
            req.UseBinary = true;
            req.KeepAlive = false;
            req.Timeout = 15000;

            using (var resp = (System.Net.FtpWebResponse)await req.GetResponseAsync())
            using (var ftpStream = resp.GetResponseStream())
            using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await ftpStream.CopyToAsync(fileStream);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private async Task DownloadFileAsync(BackupFileItem? file)
    {
        if (file == null) return;
        
        IsLoading = true;
        try
        {
            string targetFolder = @"D:\AlphaManagerBackups";
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            string localPath = Path.Combine(targetFolder, file.Name);

            bool success = await DownloadFileViaFtpAsync(file.Name, localPath);
            if (success)
            {
                await _dialogService.ShowAlertAsync($"تم تنزيل النسخة الاحتياطية بنجاح وحفظها على الكمبيوتر في:\n{localPath}", "نجاح التنزيل");
            }
            else
            {
                await _dialogService.ShowAlertAsync($"فشل في تنزيل النسخة الاحتياطية من الراوتر.\nيرجى التحقق من تفعيل خدمة FTP وصلاحيات مجلد الوجهة.", "خطأ");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"حدث خطأ أثناء تنزيل النسخة الاحتياطية: {ex.Message}", "خطأ");
        }
        finally
        {
            IsLoading = false;
        }
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
            
            // الانتظار ثانية للكتابة في الفايل سيستم للراوتر
            await Task.Delay(1500);
            
            string backupFileName = $"{backupName}.backup";
            string targetFolder = @"D:\AlphaManagerBackups";
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            string localPath = Path.Combine(targetFolder, backupFileName);

            bool success = await DownloadFileViaFtpAsync(backupFileName, localPath);
            if (success)
            {
                await _dialogService.ShowAlertAsync($"تم إنشاء النسخة الاحتياطية وحفظها على الكمبيوتر (القرص D) بنجاح:\n{localPath}", "نجاح");
            }
            else
            {
                await _dialogService.ShowAlertAsync($"تم إنشاء النسخة الاحتياطية على المايكروتك بنجاح، ولكن تعذر تنزيلها للكمبيوتر. يرجى محاولة تنزيلها يدوياً.", "تنبيه");
            }

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
            
            await Task.Delay(1500);

            string exportFileName = $"{exportName}.rsc";
            string targetFolder = @"D:\AlphaManagerBackups";
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            string localPath = Path.Combine(targetFolder, exportFileName);

            bool success = await DownloadFileViaFtpAsync(exportFileName, localPath);
            if (success)
            {
                await _dialogService.ShowAlertAsync($"تم تصدير ملف الإعدادات وحفظه على الكمبيوتر (القرص D) بنجاح:\n{localPath}", "نجاح");
            }
            else
            {
                await _dialogService.ShowAlertAsync($"تم تصدير ملف الإعدادات على المايكروتك بنجاح، ولكن تعذر تنزيلها للكمبيوتر. يرجى محاولة تنزيلها يدوياً.", "تنبيه");
            }

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



