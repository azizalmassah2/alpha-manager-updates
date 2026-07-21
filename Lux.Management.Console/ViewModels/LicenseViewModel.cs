using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Models;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Infrastructure.Services;

namespace Lux.Management.Console.ViewModels;

public partial class LicenseViewModel : ViewModelBase
{
    private readonly ILicenseService _licenseService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly ISecureStorageService _secureStorageService;
    private readonly IUserNotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHotspotService _hotspotService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayFilePath))]
    private string _selectedFilePath = string.Empty;

    public string DisplayFilePath => string.IsNullOrEmpty(SelectedFilePath) ? "لم يتم اختيار ملف ترخيص بعد..." : SelectedFilePath;

    [ObservableProperty]
    private string _routerName = "غير متوفر";

    [ObservableProperty]
    private string _routerIdentity = "غير متوفر";

    [ObservableProperty]
    private string _routerIp = "غير متوفر";

    [ObservableProperty]
    private string _routerVersion = "غير متوفر";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedHardwareId))]
    private string _hardwareId = "⏳ جاري قراءة البصمة...";

    [ObservableProperty]
    private string _serialRaw = "غير متوفر";

    [ObservableProperty]
    private bool _isRouterConnected;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLicenseValid;

    [ObservableProperty]
    private string _licenseOwner = "غير متوفر";

    [ObservableProperty]
    private string _licenseType = "غير متوفر";

    [ObservableProperty]
    private string _licenseIssueDate = "غير متوفر";

    [ObservableProperty]
    private string _licenseExpiryDate = "غير متوفر";

    [ObservableProperty]
    private string _licenseStatus = "غير متوفر";

    [ObservableProperty]
    private string _routerModel = "غير متوفر";

    [ObservableProperty]
    private string _routerArchitecture = "غير متوفر";

    [ObservableProperty]
    private int _daysRemaining;

    [ObservableProperty]
    private string _linkedRouter = "غير متوفر";

    public string FormattedHardwareId
    {
        get
        {
            if (string.IsNullOrEmpty(HardwareId) || HardwareId.StartsWith("⏳") || HardwareId.StartsWith("⚠️"))
                return HardwareId;

            if (HardwareId.Length == 64)
            {
                var blocks = System.Linq.Enumerable.Range(0, 8)
                                       .Select(i => HardwareId.Substring(i * 8, 8));
                return string.Join("-", blocks);
            }
            return HardwareId;
        }
    }

    public LicenseViewModel(
        IPermissionService permissionService,
        IEventBus eventBus,
        ILicenseService licenseService,
        IActiveRouterContext activeRouterContext,
        ISecureStorageService secureStorageService,
        IUserNotificationService notificationService,
        IServiceProvider serviceProvider,
        IHotspotService hotspotService) 
        : base(permissionService, eventBus)
    {
        _licenseService = licenseService;
        _activeRouterContext = activeRouterContext;
        _secureStorageService = secureStorageService;
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;
        _hotspotService = hotspotService;

        Title = "الترخيص والتسجيل";

        _activeRouterContext.ActiveRouterChanged += ActiveRouterContext_ActiveRouterChanged;
        
        LoadActiveLicenseDetails();
        _ = LoadRouterDetailsAsync();
    }

    private void ActiveRouterContext_ActiveRouterChanged(object? sender, EventArgs e)
    {
        App.Current.Dispatcher.InvokeAsync(async () =>
        {
            await LoadRouterDetailsAsync();
        });
    }

    public async Task LoadRouterDetailsAsync()
    {
        var router = _activeRouterContext.CurrentRouter;
        if (router == null || !_activeRouterContext.IsConnected)
        {
            IsRouterConnected = false;
            RouterName = "غير متوفر";
            RouterIdentity = "غير متوفر";
            RouterIp = "غير متوفر";
            RouterVersion = "غير متوفر";
            RouterModel = "غير متوفر";
            RouterArchitecture = "غير متوفر";
            SerialRaw = "غير متوفر";
            return;
        }

        IsRouterConnected = true;
        RouterName = router.DisplayName;
        RouterIp = router.Host;

        IsLoading = true;
        HardwareId = "⏳ جاري جلب البصمة من الراوتر...";
        RouterIdentity = "جاري التحميل...";
        RouterVersion = "جاري التحميل...";
        RouterModel = "جاري التحميل...";
        RouterArchitecture = "جاري التحميل...";
        SerialRaw = "جاري التحميل...";

        try
        {
            string password = "";
            if (!string.IsNullOrEmpty(router.EncryptedPassword))
            {
                password = _secureStorageService.Decrypt(router.EncryptedPassword);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var sysInfo = await LicenseService.GetRouterSystemInfoAsync(router.Host, password, cts.Token);
            var hwid = await LicenseService.GetHardwareIdAsync(router.Host, password, cts.Token);

            if (sysInfo != null)
            {
                RouterIdentity = string.IsNullOrEmpty(sysInfo.Identity) ? "غير متوفر" : sysInfo.Identity;
                RouterVersion = string.IsNullOrEmpty(sysInfo.Version) ? "غير متوفر" : sysInfo.Version;
                RouterModel = string.IsNullOrEmpty(sysInfo.Model) ? "غير متوفر" : sysInfo.Model;
                RouterArchitecture = string.IsNullOrEmpty(sysInfo.Architecture) ? "غير متوفر" : sysInfo.Architecture;
                SerialRaw = string.IsNullOrEmpty(sysInfo.SerialNumber) ? "غير متوفر" : sysInfo.SerialNumber;
            }

            if (!string.IsNullOrEmpty(hwid))
            {
                HardwareId = hwid;
            }
            else
            {
                HardwareId = "⚠️ فشل قراءة بصمة الراوتر - تحقق من صلاحيات API";
            }
        }
        catch
        {
            HardwareId = "⚠️ خطأ في الاتصال بالراوتر";
            RouterIdentity = "غير متوفر";
            RouterVersion = "غير متوفر";
            RouterModel = "غير متوفر";
            RouterArchitecture = "غير متوفر";
            SerialRaw = "غير متوفر";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadActiveLicenseDetails()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.dat");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var license = System.Text.Json.JsonSerializer.Deserialize<LicenseInfo>(json);
                if (license != null)
                {
                    IsLicenseValid = true;
                    LicenseOwner = string.IsNullOrEmpty(license.CustomerName) ? "غير متوفر" : license.CustomerName;
                    LicenseType = license.LicenseType switch
                    {
                        "Lifetime" => "مدى الحياة (Lifetime) 🛡️",
                        "Yearly" => "سنوي (Yearly) 📅",
                        "Monthly" => "شهري (Monthly) 📅",
                        "Trial" => "تجريبي (Trial) ⏳",
                        _ => license.LicenseType
                    };
                    LicenseIssueDate = license.IssueDate.ToLocalTime().ToString("yyyy/MM/dd");
                    LicenseExpiryDate = license.ExpiryDate.ToLocalTime().ToString("yyyy/MM/dd");
                    var expiryLocal = license.ExpiryDate.ToLocalTime().Date;
                    LicenseStatus = DateTime.Today > expiryLocal ? "منتهي الصلاحية ❌" : "نشط وصالح ✓";
                    
                    DaysRemaining = Math.Max(0, (expiryLocal - DateTime.Today).Days);
                    LinkedRouter = string.IsNullOrEmpty(license.HardwareId) ? "غير متوفر" : license.HardwareId;
                    return;
                }
            }
            catch { }
        }
        IsLicenseValid = false;
        LicenseOwner = "غير متوفر";
        LicenseType = "غير متوفر";
        LicenseIssueDate = "غير متوفر";
        LicenseExpiryDate = "غير متوفر";
        LicenseStatus = "غير مرخص";
        DaysRemaining = 0;
        LinkedRouter = "غير متوفر";
    }

    [RelayCommand]
    private void OpenConnectionDialog()
    {
        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        mainVm.IsConnectionDialogVisible = true;
    }

    [RelayCommand]
    private void CopyHwid()
    {
        if (string.IsNullOrEmpty(HardwareId) || HardwareId.StartsWith("⚠️") || HardwareId.StartsWith("⏳")) return;
        try
        {
            Clipboard.SetText(HardwareId);
            _notificationService.ShowSuccess("تم نسخ بصمة الراوتر إلى الحافظة!");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"فشل النسخ: {ex.Message}");
        }
    }

    [RelayCommand]
    private void BrowseLicense()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "License File (*.dat)|*.dat",
            Title = "اختر ملف الترخيص (license.dat)"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFilePath = dialog.FileName;
            _notificationService.ShowInformation("تم اختيار الملف — اضغط تفعيل لإكمال العملية");
        }
    }

    [RelayCommand]
    private async Task ActivateLicense()
    {
        if (string.IsNullOrEmpty(SelectedFilePath))
        {
            _notificationService.ShowError("الرجاء اختيار ملف الترخيص (license.dat) أولاً.");
            return;
        }

        var router = _activeRouterContext.CurrentRouter;
        if (router == null || !_activeRouterContext.IsConnected)
        {
            _notificationService.ShowError("يجب أن يكون الراوتر متصلاً لإتمام عملية التفعيل.");
            return;
        }

        try
        {
            string password = "";
            if (!string.IsNullOrEmpty(router.EncryptedPassword))
            {
                password = _secureStorageService.Decrypt(router.EncryptedPassword);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var success = await _licenseService.ActivateAsync(
                routerIp: router.Host,
                apiPassword: password,
                licenseFilePath: SelectedFilePath,
                ct: cts.Token);

            if (success)
            {
                try
                {
                    var licenseBytes = await File.ReadAllBytesAsync(SelectedFilePath, cts.Token);
                    var uploaded = await _hotspotService.UploadFileFtpAsync(router.Host, router.Username, password, licenseBytes, "license/license.dat");
                    if (uploaded)
                    {
                        _notificationService.ShowSuccess("تم تفعيل الترخيص ورفعه للراوتر بنجاح!");
                    }
                    else
                    {
                        _notificationService.ShowWarning("تم تفعيل الترخيص محلياً ولكن فشل رفعه للراوتر. يرجى التأكد من صلاحيات FTP بالراوتر.");
                    }
                }
                catch (Exception uploadEx)
                {
                    _notificationService.ShowWarning($"تم التفعيل محلياً ولكن فشل الرفع للراوتر: {uploadEx.Message}");
                }

                var shellState = _serviceProvider.GetRequiredService<IShellState>();
                shellState.IsRegistered = true;

                LoadActiveLicenseDetails();
            }
            else
            {
                _notificationService.ShowError("فشل التفعيل — ملف الترخيص غير صالح أو لا يطابق الراوتر.");
                
                // عرض تفاصيل التشخيص التفصيلية لتحديد سبب فشل التفعيل بدقة
                var diag = _licenseService.LastDiagnostics;
                System.Windows.MessageBox.Show(
                    diag, 
                    "تفاصيل تشخيص تفعيل الترخيص", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"خطأ أثناء التفعيل: {ex.Message}");
        }
    }

    public override void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= ActiveRouterContext_ActiveRouterChanged;
        base.Dispose();
    }
}
