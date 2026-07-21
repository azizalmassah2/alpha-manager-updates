using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core.ViewModels;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Application.Interfaces;
using Lux.Platform.Abstractions.Models.Monitoring;
using Lux.Management.Console.Core.Security.Authorization;
using Lux.Management.Console.Core.Security.Models;

using Lux.Management.Console.Modules.MikroTik.Dashboard;
using Lux.Management.Console.Modules.Monitoring;
using MikroTikVoucherPrinter.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.Management.Console.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly Lux.Management.Console.Core.INavigationService _navigationService;
    private readonly IThemeManager _themeManager;
    private readonly IShellState _shellState;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IBusyIndicatorService _busyIndicatorService;
    private readonly ISettingsService _settingsService;
    private readonly IRouterRepository _routerRepository;
    private readonly IAlertService _alertService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly Lux.Management.Console.Core.Session.ISessionManager _sessionManager;
    private readonly Lux.Management.Console.Core.Session.IConnectionService _connectionService;
    private readonly Lux.Management.Console.Core.Session.IRouterSessionService _routerSessionService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly IFeatureAuthorizationService _featureAuthorizationService;
    private readonly IUserNotificationService _notificationService;
    private Action? _pendingNavigation;

    public IShellState ShellState => _shellState;
    public Lux.Management.Console.Core.INavigationService Navigation => _navigationService;
    public IBusyIndicatorService BusyIndicator => _busyIndicatorService;

    public ActiveRouterStatusViewModel ActiveRouterStatus { get; }
    public Lux.Management.Console.Modules.MikroTik.Connections.Dialog.MikroTikConnectionDialogViewModel ConnectionDialogViewModel { get; }

    [ObservableProperty]
    private bool _isConnectionDialogVisible;

    [ObservableProperty]
    private bool _isSidebarExpanded = false;

    [ObservableProperty]
    private ObservableCollection<Alert> _alerts = new();

    [ObservableProperty]
    private int _unreadAlertsCount;

    [RelayCommand]
    private void ShowConnectionDialog()
    {
        IsConnectionDialogVisible = true;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        _settingsService.Set("SidebarExpanded", IsSidebarExpanded);
        _ = _settingsService.SaveAsync();
    }

    public MainViewModel(
        IPermissionService permissionService, 
        IEventBus eventBus,
        Lux.Management.Console.Core.INavigationService navigationService,
        IThemeManager themeManager,
        IShellState shellState,
        IBusyIndicatorService busyIndicatorService,
        ActiveRouterStatusViewModel activeRouterStatus,
        IActiveRouterContext activeRouterContext,
        Lux.Management.Console.Modules.MikroTik.Connections.Dialog.MikroTikConnectionDialogViewModel connectionDialogViewModel,
        ISettingsService settingsService,
        IRouterRepository routerRepository,
        IAlertService alertService,
        ILogger<MainViewModel> logger,
        Lux.Management.Console.Core.Session.ISessionManager sessionManager,
        Lux.Management.Console.Core.Session.IConnectionService connectionService,
        Lux.Management.Console.Core.Session.IRouterSessionService routerSessionService,
        ISecureStorageService secureStorageService,
        IFeatureAuthorizationService featureAuthorizationService,
        IUserNotificationService notificationService) 
        : base(permissionService, eventBus)
    {
        _navigationService = navigationService;
        _themeManager = themeManager;
        _shellState = shellState;
        _busyIndicatorService = busyIndicatorService;
        ActiveRouterStatus = activeRouterStatus;
        _activeRouterContext = activeRouterContext;
        ConnectionDialogViewModel = connectionDialogViewModel;
        _settingsService = settingsService;
        _routerRepository = routerRepository;
        _alertService = alertService;
        _logger = logger;
        _sessionManager = sessionManager;
        _connectionService = connectionService;
        _routerSessionService = routerSessionService;
        _secureStorageService = secureStorageService;
        _featureAuthorizationService = featureAuthorizationService;
        _notificationService = notificationService;

        _ = LoadAlertsAsync();

        // Auto-close dialog and resume navigation when connected
        _activeRouterContext.ActiveRouterChanged += async (s, e) =>
        {
            if (_activeRouterContext.IsConnected && _activeRouterContext.CurrentRouter != null)
            {
                // Save last connected router to settings
                if (_activeRouterContext.CurrentRouterId.HasValue)
                {
                    _settingsService.Set("LastConnectedRouterId", _activeRouterContext.CurrentRouterId.Value);
                    await _settingsService.SaveAsync();
                }

                // ── تحديث الجلسة والترخيص عند التبديل داخل لوحة التحكم ──
                try
                {
                    var router = _activeRouterContext.CurrentRouter;
                    string password = string.Empty;
                    if (!string.IsNullOrEmpty(router.EncryptedPassword))
                    {
                        password = _secureStorageService.Decrypt(router.EncryptedPassword);
                    }

                    var infoResult = await _routerSessionService.ConnectAndGetInfoAsync(router.Host, router.Port, router.Username, password);
                    if (infoResult.IsSuccess && infoResult.RouterInfo != null)
                    {
                        var licResult = await _connectionService.VerifyLicenseAsync(infoResult.RouterInfo);
                        var session = await _connectionService.CreateSessionAsync(infoResult.RouterInfo, licResult);
                        _sessionManager.SetSession(session);
                        _shellState.IsRegistered = session.IsProMode;

                        // التحقق من اقتراب انتهاء الترخيص وإظهار تنبيه عند تبديل الراوتر
                        if (session.IsProMode && session.LicenseExpiresAt.HasValue)
                        {
                            DateTime expiryLocal = session.LicenseExpiresAt.Value.ToLocalTime().Date;
                            int daysRemaining = (expiryLocal - DateTime.Today).Days;
                            if (daysRemaining >= 0 && daysRemaining < 10)
                            {
                                _notificationService.ShowWarning(
                                    $"⚠️ تنبيه: الترخيص الخاص بالراوتر المتصل أوشك على الانتهاء! متبقي له {daysRemaining} يوم/أيام فقط (ينتهي بتاريخ {expiryLocal:yyyy/MM/dd}). يرجى تجديد الترخيص لضمان استمرار تشغيل الوضع الاحترافي.",
                                    "تنبيه انتهاء الترخيص");
                            }
                        }
                    }
                }
                catch { }

                if (IsConnectionDialogVisible)
                {
                    IsConnectionDialogVisible = false;
                    if (_pendingNavigation != null)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            _pendingNavigation.Invoke();
                            _pendingNavigation = null;
                        });
                    }
                }
            }
            else if (!_activeRouterContext.IsConnected)
            {
                // قطع الاتصال -> جلسة مجانية بدون راوتر
                var session = await _connectionService.CreateSessionAsync(null, null);
                _sessionManager.SetSession(session);
                _shellState.IsRegistered = false;
            }
        };

        ConnectionDialogViewModel.CloseAction = () =>
        {
            IsConnectionDialogVisible = false;
            _pendingNavigation = null;
        };

        // Load settings and navigate to dashboard
        _ = InitializeSettingsAsync();
    }

    private async Task InitializeSettingsAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            IsSidebarExpanded = _settingsService.Get("SidebarExpanded", false);
        }
        catch { }

        // التوجيه التلقائي إلى لوحة التحكم بعد تسجيل الدخول الناجح
        App.Current.Dispatcher.Invoke(() => 
        {
            _navigationService.Navigate<DashboardViewModel>();
        });
    }

    [RelayCommand]
    private void NavigateHome()
    {
        _navigationService.Navigate<Lux.Management.Console.Modules.MikroTik.Dashboard.DashboardViewModel>();
    }

    [RelayCommand]
    private void NavigateMikroTikCenter()
    {
        if (!_activeRouterContext.IsConnected)
        {
            _pendingNavigation = () => _navigationService.Navigate<Lux.Management.Console.Modules.MikroTik.ViewModels.MikroTikCenterViewModel>();
            IsConnectionDialogVisible = true;
            return;
        }
        
        _navigationService.Navigate<Lux.Management.Console.Modules.MikroTik.ViewModels.MikroTikCenterViewModel>();
    }

    [RelayCommand]
    private void NavigateBroadcastingCenter()
    {
        if (!_featureAuthorizationService.HasFeature(FeatureId.Broadcasting))
        {
            System.Windows.MessageBox.Show(
                "ميزة أجهزة البث غير متوفرة في النسخة المجانية.\nيرجى تفعيل البرنامج للوصول لكافة الميزات.",
                "ميزة مقفلة",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }
        _navigationService.Navigate<Lux.Management.Console.Modules.Broadcasting.ViewModels.BroadcastingCenterViewModel>();
    }

    [RelayCommand]
    private void NavigateMonitoringCenter()
    {
        if (!_featureAuthorizationService.HasFeature(FeatureId.NetworkMonitoring))
        {
            System.Windows.MessageBox.Show(
                "ميزة مراقبة الفيلانات والشبكة غير متوفرة في النسخة المجانية.\nيرجى تفعيل البرنامج للوصول لكافة الميزات.",
                "ميزة مقفلة",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }
        _navigationService.Navigate<DevicesMonitorViewModel>();
    }

    [RelayCommand]
    private void NavigateOperationsCenter()
    {
        _navigationService.Navigate<Lux.Management.Console.Modules.Operations.ViewModels.OperationsCenterViewModel>();
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        _navigationService.Navigate<Lux.Management.Console.Modules.Settings.ViewModels.SettingsViewModel>();
    }

    [RelayCommand]
    private void NavigateLicense()
    {
        _navigationService.Navigate<LicenseViewModel>();
    }

    [RelayCommand]
    private void NavigateUpdates()
    {
        _navigationService.Navigate<UpdatesViewModel>();
    }

    [RelayCommand]
    private void NavigateBackup()
    {
        _navigationService.Navigate<Lux.Management.Console.Modules.MikroTik.Backups.ViewModels.BackupsViewModel>();
    }

    [RelayCommand]
    private void NavigateAbout()
    {
        _navigationService.Navigate<AboutViewModel>();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeManager.ToggleTheme();
    }

    public async Task LoadAlertsAsync()
    {
        try
        {
            var active = await _alertService.GetActiveAlertsAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                Alerts.Clear();
                foreach (var a in active)
                {
                    Alerts.Add(a);
                }

                if (Alerts.Count == 0)
                {
                    Alerts.Add(new Alert
                    {
                        Message = "مرحباً بك في نظام Alpha Manager المطور! 🚀",
                        Timestamp = DateTime.UtcNow,
                        Severity = AlertSeverity.Information
                    });
                }

                UnreadAlertsCount = Alerts.Count(a => !a.IsAcknowledged);
            });
        }
        catch { }
    }

    [RelayCommand]
    private async Task AcknowledgeAlert(Alert alert)
    {
        if (alert == null) return;
        try
        {
            await _alertService.AcknowledgeAlertAsync(alert.Id);
            Alerts.Remove(alert);
            UnreadAlertsCount = Alerts.Count(a => !a.IsAcknowledged);
        }
        catch { }
    }
}
