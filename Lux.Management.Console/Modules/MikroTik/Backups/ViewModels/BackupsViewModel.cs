using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikroTikVoucherPrinter.Domain.Interfaces;
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
    public string FilePath { get; set; } = string.Empty;
}

public partial class BackupsViewModel : ViewModelBase, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;
    private readonly Lux.Management.Console.Core.IDialogService _dialogService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ObservableCollection<BackupFileItem> _files = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public BackupsViewModel(
        IActiveRouterContext activeRouterContext, 
        IRouterManagementService routerService, 
        Lux.Management.Console.Core.IDialogService dialogService,
        IPermissionService permissionService,
        IEventBus eventBus,
        ISecureStorageService secureStorageService,
        ISettingsService settingsService)
        : base(permissionService, eventBus)
    {
        _activeRouterContext = activeRouterContext;
        _routerService = routerService;
        _dialogService = dialogService;
        _secureStorageService = secureStorageService;
        _settingsService = settingsService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        
        var _ = LoadDataAsync();
    }

    private string GetBackupPath()
    {
        var path = _settingsService.Get<string>("BackupPath");
        if (string.IsNullOrEmpty(path))
        {
            return GetDefaultBackupPath();
        }
        return path;
    }

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

    private async Task<bool> DownloadFileWithRetryAsync(string remoteFileName, string localFilePath, int maxRetries = 5, int delayMs = 2000)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            bool success = await DownloadFileViaFtpAsync(remoteFileName, localFilePath);
            if (success) return true;
            await Task.Delay(delayMs);
        }
        return false;
    }

    private async Task<string?> FindRemoteFilePathAsync(string pattern)
    {
        try
        {
            var response = await _routerService.ExecuteQueryAsync("/file/print");
            var match = response.RawData.FirstOrDefault(d => d.GetValueOrDefault("name", "").Contains(pattern));
            return match?.GetValueOrDefault("name", null);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> FindRemoteFilePathWithRetryAsync(string pattern, int maxRetries = 25, int delayMs = 1500)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var path = await FindRemoteFilePathAsync(pattern);
            if (path != null) return path;
            await Task.Delay(delayMs);
        }
        return null;
    }

    private async Task<bool> UploadFileViaFtpAsync(string localFilePath, string remoteFileName)
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
            req.Method = System.Net.WebRequestMethods.Ftp.UploadFile;
            req.Credentials = new System.Net.NetworkCredential(router.Username, password);
            req.UsePassive = true;
            req.UseBinary = true;
            req.KeepAlive = false;
            req.Timeout = 20000;

            using (var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var ftpStream = await req.GetRequestStreamAsync())
            {
                await fileStream.CopyToAsync(ftpStream);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            string targetFolder = GetBackupPath();
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            var localFiles = Directory.GetFiles(targetFolder)
                .Select(path => new FileInfo(path))
                .Where(f => f.Extension.Equals(".backup", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".rsc", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".umb", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.CreationTime)
                .Select(f => {
                    string type = "MikroTik";
                    if (f.Name.StartsWith("usermanager_", StringComparison.OrdinalIgnoreCase) || 
                        f.Extension.Equals(".umb", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "User Manager";
                    }

                    return new BackupFileItem
                    {
                        Id = f.Name,
                        Name = f.Name,
                        Type = type,
                        Size = FormatBytes(f.Length.ToString()),
                        CreationTime = f.CreationTime.ToString("yyyy/MM/dd HH:mm:ss"),
                        FilePath = f.FullName
                    };
                }).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Files.Clear();
                foreach (var item in localFiles) Files.Add(item);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"فشل في قراءة الملفات المحلية: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<List<string>> GetRouterFilesAsync()
    {
        try
        {
            var response = await _routerService.ExecuteQueryAsync("/file/print");
            return response.RawData
                .Select(d => d.GetValueOrDefault("name", ""))
                .Where(name => name.EndsWith(".backup", StringComparison.OrdinalIgnoreCase) || 
                               name.EndsWith(".umb", StringComparison.OrdinalIgnoreCase) || 
                               name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task<string?> FindNewFileWithRetryAsync(List<string> beforeFiles, int maxRetries = 4, int delayMs = 1000)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var afterFiles = await GetRouterFilesAsync();
            var newFile = afterFiles.Except(beforeFiles).FirstOrDefault();
            if (newFile != null) return newFile;
            await Task.Delay(delayMs);
        }
        return null;
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (!_activeRouterContext.IsConnected)
        {
            await _dialogService.ShowAlertAsync("يرجى الاتصال بالراوتر أولاً لإنشاء نسخة احتياطية.", "خطأ");
            return;
        }

        IsLoading = true;
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string routerBackupName = $"system_{timestamp}";
        string routerUmName = $"usermanager_{timestamp}";

        bool isV7 = _activeRouterContext.CurrentRouter?.RouterOSVersion?.StartsWith("7") == true;
        string umExtension = isV7 ? ".rsc" : ".umb";

        string targetFolder = GetBackupPath();
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        int successCount = 0;
        List<string> logs = new();

        // 1. Create and download MikroTik system backup
        try
        {
            var beforeSystemFiles = await GetRouterFilesAsync();

            await _routerService.ExecuteCommandAsync("/system/backup/save", new Dictionary<string, string> 
            { 
                { "name", routerBackupName },
                { "dont-encrypt", "yes" }
            });
            
            // البحث التلقائي ومقارنة الملفات للعثور على النسخة الاحتياطية الجديدة (مهلة 4 ثوانٍ كحد أقصى)
            string? remoteBackupFile = await FindNewFileWithRetryAsync(beforeSystemFiles, 4, 1000);
            if (!string.IsNullOrEmpty(remoteBackupFile))
            {
                string localFileName = Path.GetFileName(remoteBackupFile);
                string localBackupPath = Path.Combine(targetFolder, localFileName);

                bool downloaded = await DownloadFileWithRetryAsync(remoteBackupFile, localBackupPath, 4, 1000);
                if (downloaded)
                {
                    successCount++;
                    logs.Add("✓ تم إنشاء وتنزيل نسخة النظام (MikroTik Settings).");
                    try { await _routerService.ExecuteCommandAsync("/file/remove", new Dictionary<string, string> { { "numbers", remoteBackupFile } }); } catch { }
                }
                else
                {
                    logs.Add("❌ فشل تنزيل نسخة النظام من الراوتر via FTP.");
                }
            }
            else
            {
                logs.Add("❌ لم يتم العثور على ملف نسخة النظام على الراوتر (انتهاء المهلة).");
            }
        }
        catch (Exception ex)
        {
            logs.Add($"❌ فشل إنشاء نسخة النظام: {ex.Message}");
        }

        // 2. Create and download User Manager database backup
        try
        {
            var beforeUmFiles = await GetRouterFilesAsync();

            if (isV7)
            {
                await _routerService.ExecuteCommandAsync("/user-manager/export", new Dictionary<string, string> { { "file", routerUmName } });
            }
            else
            {
                await _routerService.ExecuteCommandAsync("/tool/user-manager/database/save", new Dictionary<string, string> { { "name", routerUmName } });
            }

            // البحث التلقائي ومقارنة الملفات للعثور على نسخة User Manager الجديدة (مهلة 4 ثوانٍ كحد أقصى)
            string? remoteUmFile = await FindNewFileWithRetryAsync(beforeUmFiles, 4, 1000);
            if (!string.IsNullOrEmpty(remoteUmFile))
            {
                string localFileName = Path.GetFileName(remoteUmFile);
                string localUmPath = Path.Combine(targetFolder, localFileName);

                bool downloaded = await DownloadFileWithRetryAsync(remoteUmFile, localUmPath, 4, 1000);
                if (downloaded)
                {
                    successCount++;
                    logs.Add("✓ تم إنشاء وتنزيل نسخة User Manager.");
                    try { await _routerService.ExecuteCommandAsync("/file/remove", new Dictionary<string, string> { { "numbers", remoteUmFile } }); } catch { }
                }
                else
                {
                    logs.Add("❌ فشل تنزيل نسخة User Manager من الراوتر via FTP.");
                }
            }
            else
            {
                logs.Add("❌ لم يتم العثور على ملف نسخة User Manager على الراوتر (انتهاء المهلة).");
            }
        }
        catch (Exception ex)
        {
            logs.Add($"❌ فشل إنشاء نسخة User Manager: {ex.Message}");
        }

        await LoadDataAsync();

        string summary = string.Join("\n", logs);
        if (successCount == 2)
        {
            await _dialogService.ShowAlertAsync($"تم إنجاز النسخ الاحتياطي بالكامل وحفظه بالكمبيوتر:\n\n{summary}", "نجاح");
        }
        else if (successCount > 0)
        {
            await _dialogService.ShowAlertAsync($"اكتمل النسخ الاحتياطي جزئياً:\n\n{summary}", "تنبيه");
        }
        else
        {
            await _dialogService.ShowAlertAsync($"فشل إنشاء النسخ الاحتياطية:\n\n{summary}", "خطأ");
        }

        IsLoading = false;
    }

    [RelayCommand]
    private async Task RestoreFileAsync(BackupFileItem? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath) || !File.Exists(file.FilePath))
        {
            await _dialogService.ShowAlertAsync("الملف المحلي غير موجود أو لم يتم تحديده.", "خطأ");
            return;
        }

        if (!_activeRouterContext.IsConnected)
        {
            await _dialogService.ShowAlertAsync("يرجى الاتصال بالراوتر أولاً لاستعادة النسخة الاحتياطية.", "خطأ");
            return;
        }

        bool confirm = await _dialogService.ShowConfirmationAsync(
            $"⚠️ تنبيه: هل أنت متأكد من استعادة النسخة الاحتياطية '{file.Name}' على الراوتر؟\n\n" +
            "ستقوم هذه العملية باستبدال البيانات الحالية على الراوتر. وفي حال نسخ النظام الكلية، سيتم إعادة تشغيل الراوتر تلقائياً.");

        if (!confirm) return;

        IsLoading = true;
        try
        {
            // 1. Upload local file to router via FTP
            bool uploaded = await UploadFileViaFtpAsync(file.FilePath, file.Name);
            if (!uploaded)
            {
                await _dialogService.ShowAlertAsync("فشل في رفع ملف النسخة الاحتياطية إلى الراوتر عبر FTP.", "خطأ");
                return;
            }

            // 2. Execute restore command
            if (file.Type == "User Manager")
            {
                if (file.Name.EndsWith(".umb", StringComparison.OrdinalIgnoreCase))
                {
                    await _routerService.ExecuteCommandAsync("/tool/user-manager/database/load", new Dictionary<string, string> { { "name", file.Name } });
                }
                else if (file.Name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
                {
                    await _routerService.ExecuteCommandAsync("/import", new Dictionary<string, string> { { "file-name", file.Name } });
                }
                
                // Cleanup file on router
                try { await _routerService.ExecuteCommandAsync("/file/remove", new Dictionary<string, string> { { "numbers", file.Name } }); } catch { }
                await _dialogService.ShowAlertAsync("تم استعادة قاعدة بيانات User Manager بنجاح.", "نجاح");
            }
            else // MikroTik settings
            {
                if (file.Name.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await _routerService.ExecuteCommandAsync("/system/backup/load", new Dictionary<string, string> { { "name", file.Name } });
                    }
                    catch
                    {
                        // المتوقع حدوث قطع اتصال بسبب إعادة التشغيل التلقائي للراوتر
                    }
                    await _dialogService.ShowAlertAsync("تم إرسال ملف الاستعادة وجاري إعادة تشغيل الراوتر الآن...", "جاري الاستعادة");
                }
                else if (file.Name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
                {
                    await _routerService.ExecuteCommandAsync("/import", new Dictionary<string, string> { { "file-name", file.Name } });
                    try { await _routerService.ExecuteCommandAsync("/file/remove", new Dictionary<string, string> { { "numbers", file.Name } }); } catch { }
                    await _dialogService.ShowAlertAsync("تم استيراد إعدادات النظام بنجاح.", "نجاح");
                }
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل أثناء استعادة النسخة الاحتياطية: {ex.Message}", "خطأ");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteFileAsync(BackupFileItem? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath)) return;

        bool confirm = await _dialogService.ShowConfirmationAsync($"هل أنت متأكد من حذف الملف {file.Name} نهائياً من جهاز الكمبيوتر؟");
        if (!confirm) return;

        try
        {
            if (File.Exists(file.FilePath))
            {
                File.Delete(file.FilePath);
            }
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"فشل في حذف الملف المحلي: {ex.Message}", "خطأ");
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

    public override void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}



