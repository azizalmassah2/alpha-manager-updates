using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Management.Console.Core.Session;
using Lux.Management.Console.Modules.MikroTik.Connections.Services;
using Lux.MikroTik.Models;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.ViewModels;

public partial class LoginViewModel : ObservableObject, IDisposable
{
    private readonly IConnectionService _connectionService;
    private readonly ISessionManager _sessionManager;
    private readonly IShellState _shellState;
    private readonly IMikroTikDiscoveryService _discoveryService;
    private readonly IRouterRepository _routerRepository;
    private readonly ISecureStorageService _secureStorageService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly ISettingsService _settingsService;
    private readonly CancellationTokenSource _cts = new();

    public LoginViewModel(
        IConnectionService connectionService,
        ISessionManager sessionManager,
        IShellState shellState,
        IMikroTikDiscoveryService discoveryService,
        IRouterRepository routerRepository,
        ISecureStorageService secureStorageService,
        IActiveRouterContext activeRouterContext,
        ISettingsService settingsService)
    {
        _connectionService = connectionService;
        _sessionManager = sessionManager;
        _shellState = shellState;
        _discoveryService = discoveryService;
        _routerRepository = routerRepository;
        _secureStorageService = secureStorageService;
        _activeRouterContext = activeRouterContext;
        _settingsService = settingsService;

        // تحميل الإعدادات والراوترات المحفوظة والبحث عن الأجهزة تلقائياً
        _ = InitializeAsync();
    }

    public string AppVersion => GetAppVersion();

    private string GetAppVersion()
    {
        try
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    // ── Form Fields ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _host = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _port = "8728";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _username = "admin";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe = true;

    // ── State ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ── Saved Routers ─────────────────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<Router> _savedRouters = new();

    [ObservableProperty]
    private Router? _selectedSavedRouter;

    // ── Discovered Devices (MNDP) ─────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<DiscoveredDevice> _discoveredDevices = new();

    private DiscoveredDevice? _selectedDevice;
    public DiscoveredDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value) && value != null)
            {
                Host = value.IpAddress;
                Port = "8728";
                Username = "admin";
                Password = string.Empty;
            }
        }
    }

    // ── Actions/Events ────────────────────────────────────────────────────────

    public Action? RequestClose { get; set; }
    public Action<ApplicationSession>? LoginSucceeded { get; set; }

    // ── Methods ───────────────────────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        await LoadSavedRoutersAsync();
        await AttemptAutoReconnectFromSettingsAsync();
        _ = RefreshDiscoveryAsync();
    }

    private async Task LoadSavedRoutersAsync()
    {
        try
        {
            var list = await _routerRepository.GetAllAsync();
            SavedRouters = new ObservableCollection<Router>(list);
        }
        catch { }
    }

    private async Task AttemptAutoReconnectFromSettingsAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            bool autoConnect = _settingsService.Get("AutoConnectOnStartup", true);
            if (!autoConnect) return;

            var lastRouterIdStr = _settingsService.Get<string>("LastConnectedRouterId", null!);
            if (!string.IsNullOrEmpty(lastRouterIdStr) && Guid.TryParse(lastRouterIdStr, out var lastRouterId))
            {
                var router = SavedRouters.FirstOrDefault(r => r.Id == lastRouterId);
                if (router != null)
                {
                    Host = router.Host;
                    Port = router.Port.ToString();
                    Username = router.Username;
                    if (!string.IsNullOrEmpty(router.EncryptedPassword))
                    {
                        Password = _secureStorageService.Decrypt(router.EncryptedPassword);
                    }
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task RefreshDiscoveryAsync()
    {
        if (IsDiscovering) return;
        IsDiscovering = true;
        try
        {
            var list = await _discoveryService.DiscoverDevicesAsync(_cts.Token);
            var validList = list.Where(d => !string.IsNullOrWhiteSpace(d.MacAddress) && 
                                            !d.MacAddress.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && 
                                            !d.MacAddress.Equals("—", StringComparison.OrdinalIgnoreCase) && 
                                            d.MacAddress != "00:00:00:00:00:00" && 
                                            d.MacAddress != "00-00-00-00-00-00").ToList();
            DiscoveredDevices = new ObservableCollection<DiscoveredDevice>(validList);
        }
        catch { }
        finally
        {
            IsDiscovering = false;
        }
    }

    private bool CanConnect()
    {
        return !string.IsNullOrWhiteSpace(Host) &&
               !string.IsNullOrWhiteSpace(Username) &&
               int.TryParse(Port, out int p) && p > 0 && p <= 65535;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        ErrorMessage = string.Empty;
        StatusMessage = "جاري الاتصال بجهاز المايكروتك...";
        IsBusy = true;

        try
        {
            int portNum = int.Parse(Port);

            // 1. الاتصال بالراوتر
            var connectResult = await _connectionService.ConnectAsync(Host, portNum, Username, Password, _cts.Token);
            if (!connectResult.IsSuccess || connectResult.RouterInfo == null)
            {
                ErrorMessage = connectResult.ErrorMessage;
                return;
            }

            // تصفير كلمة المرور فوراً من الذاكرة لتقليل عمر البيانات الحساسة
            Password = string.Empty;

            StatusMessage = "جاري التحقق من ترخيص الجهاز...";

            // 2. التحقق من الترخيص
            var licenseResult = await _connectionService.VerifyLicenseAsync(connectResult.RouterInfo, _cts.Token);

            // 3. اتخاذ القرار بناءً على الترخيص والدخول في كلتا الحالتين
            ApplicationSession session;
            if (licenseResult.IsValid)
            {
                // ترخيص صالح -> وضع احترافي
                session = await _connectionService.CreateSessionAsync(connectResult.RouterInfo, licenseResult);
            }
            else
            {
                // لا يوجد ترخيص صالح -> وضع مجاني ومواصلة الدخول بشكل طبيعي دون إظهار أي أخطاء ترخيص
                session = await _connectionService.CreateSessionAsync(connectResult.RouterInfo, null);
            }

            CompleteLogin(session);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"حدث خطأ غير متوقع: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ConnectSavedRouterAsync(Router? router)
    {
        if (router == null) return;
        Host = router.Host;
        Port = router.Port.ToString();
        Username = router.Username;
        if (!string.IsNullOrEmpty(router.EncryptedPassword))
        {
            Password = _secureStorageService.Decrypt(router.EncryptedPassword);
        }

        await ConnectAsync();
    }

    [RelayCommand]
    private async Task DeleteSavedRouterAsync(Router? router)
    {
        if (router == null) return;
        try
        {
            await _routerRepository.DeleteAsync(router.Id);
            await LoadSavedRoutersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"تعذّر الحذف: {ex.Message}";
        }
    }

    private void CompleteLogin(ApplicationSession session)
    {
        try
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
        catch { }
        IsDiscovering = false;

        // تعيين الجلسة المركزية
        _sessionManager.SetSession(session);

        // ضبط حالة الترخيص في ShellState للتوافق الخلفي
        _shellState.IsRegistered = session.IsProMode;

        // حفظ إعدادات الاتصال الأخير إذا رغب المستخدم وتم الاتصال بنجاح
        if (session.IsConnected && session.Router != null)
        {
            _settingsService.Set("LastConnectedRouterId", session.Router.RouterId.ToString());
            _ = _settingsService.SaveAsync();
        }

        // استدعاء حدث النجاح
        LoginSucceeded?.Invoke(session);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
