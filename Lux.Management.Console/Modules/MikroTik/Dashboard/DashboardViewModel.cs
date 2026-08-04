using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.MikroTik.Models;
using Lux.MikroTik.Providers;

namespace Lux.Management.Console.Modules.MikroTik.Dashboard;

public class DashboardAlertDto
{
    public string Severity { get; set; } = "تنبيه";
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IDispatcherService _dispatcherService;
    private readonly IAutoRefreshService _autoRefreshService;
    private readonly IVoucherBackgroundImportManager _backgroundImportManager;
    private readonly IRouterOsProvider _routerOsProvider;
    private readonly ISecureStorageService _secureStorageService;

    [ObservableProperty]
    private string _routerName = "—";

    [ObservableProperty]
    private string _routerHost = "—";

    [ObservableProperty]
    private string _connectionStatus = "غير متصل";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _routerBoard = "—";

    [ObservableProperty]
    private string _routerOsVersion = "—";

    [ObservableProperty]
    private string _lastConnectedTime = "—";

    [ObservableProperty] private int _totalDevices;
    [ObservableProperty] private int _onlineDevices;
    [ObservableProperty] private int _offlineDevices;
    [ObservableProperty] private int _warningDevices;
    [ObservableProperty] private int _criticalDevices;
    [ObservableProperty] private int _totalProjects;
    [ObservableProperty] private int _activeMonitoringSessions;
    [ObservableProperty] private int _liveActiveUsers;
    [ObservableProperty] private int _liveActiveHotspotUsers;
    [ObservableProperty] private int _liveActivePppUsers;
    [ObservableProperty] private string _liveActiveDetails = "هوتسبوت: 0 | PPP: 0";
    [ObservableProperty] private int _liveHosts;
    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<DashboardAlertDto> _recentAlerts = new();

    public DashboardViewModel(
        IPermissionService permissionService,
        IEventBus eventBus,
        IActiveRouterContext activeRouterContext,
        IDispatcherService dispatcherService,
        IAutoRefreshService autoRefreshService,
        IVoucherBackgroundImportManager backgroundImportManager,
        IRouterOsProvider routerOsProvider,
        ISecureStorageService secureStorageService) 
        : base(permissionService, eventBus)
    {
        _activeRouterContext = activeRouterContext;
        _dispatcherService = dispatcherService;
        _autoRefreshService = autoRefreshService;
        _backgroundImportManager = backgroundImportManager;
        _routerOsProvider = routerOsProvider;
        _secureStorageService = secureStorageService;

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        _autoRefreshService.RegisterCallback(LoadRealDataAsync);

        UpdateInfo();
        _ = LoadRealDataAsync();
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        _dispatcherService.InvokeAsync(async () =>
        {
            UpdateInfo();
            await LoadRealDataAsync();
        });
    }

    private void UpdateInfo()
    {
        IsConnected = _activeRouterContext.IsConnected;
        var router = _activeRouterContext.CurrentRouter;
        if (router != null)
        {
            RouterName = router.DisplayName;
            RouterHost = router.Host;
            RouterBoard = string.IsNullOrEmpty(router.RouterBoard) ? "—" : router.RouterBoard;
            RouterOsVersion = string.IsNullOrEmpty(router.RouterOSVersion) ? "—" : router.RouterOSVersion;
            LastConnectedTime = router.LastConnectedUtc.HasValue
                ? router.LastConnectedUtc.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
                : "—";
            ConnectionStatus = IsConnected ? "متصل ✓" : "غير متصل";
        }
        else
        {
            RouterName = "لا يوجد راوتر نشط";
            RouterHost = "—";
            RouterBoard = "—";
            RouterOsVersion = "—";
            LastConnectedTime = "—";
            ConnectionStatus = "غير متصل";
        }
    }

    private async Task LoadRealDataAsync()
    {
        var router = _activeRouterContext.CurrentRouter;
        if (router == null)
        {
            TotalDevices = 0;
            OnlineDevices = 0;
            OfflineDevices = 0;
            WarningDevices = 0;
            CriticalDevices = 0;
            TotalProjects = 0;
            ActiveMonitoringSessions = 0;
            LiveActiveUsers = 0;
            LiveHosts = 0;
            _dispatcherService.Invoke(() => RecentAlerts.Clear());
            return;
        }

        var dbPath = _backgroundImportManager.GetCachedCleanDbPath(router.Id);
        bool hasUsermanDb = !string.IsNullOrEmpty(dbPath) && File.Exists(dbPath);

        var diagnostics = new System.Collections.Generic.List<string>();
        if (!hasUsermanDb)
        {
            diagnostics.Add("قاعدة بيانات UserManager غير متوفرة محلياً (نظام Hotspot/محلي). تم تفعيل نمط العرض الحي.");
        }

        try
        {
            if (!_routerOsProvider.IsConnected)
            {
                diagnostics.Add("مزود الاتصال غير نشط، محاولة الاتصال التلقائي...");
                try
                {
                    string password = string.Empty;
                    if (!string.IsNullOrEmpty(router.EncryptedPassword))
                    {
                        password = _secureStorageService.Decrypt(router.EncryptedPassword);
                    }

                    var options = new MikroTikConnectionOptions
                    {
                        Host = router.Host,
                        Port = router.Port,
                        Username = router.Username,
                        Password = password,
                        UseSsl = false,
                        ProviderType = RouterOsProviderType.Api
                    };

                    var connRes = await _routerOsProvider.ConnectAsync(options);
                    if (connRes.IsSuccess)
                    {
                        diagnostics.Add("تم تأسيس الاتصال التلقائي بالراوتر بنجاح.");
                    }
                    else
                    {
                        diagnostics.Add($"فشل الاتصال التلقائي بالراوتر: {connRes.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.Add($"خطأ أثناء محاولة الاتصال بالراوتر: {ex.Message}");
                }
            }
            else
            {
                diagnostics.Add("الاتصال بالمايكروتيك نشط وجاهز للعمل.");
            }

            int liveActive = 0;
            int liveActiveHotspot = 0;
            int liveActivePpp = 0;
            int liveHostsVal = 0;

            if (_routerOsProvider.IsConnected)
            {
                try
                {
                    // 1. Hotspot active users count
                    var hsCmd = new MikroTikCommand { Command = "/ip/hotspot/active/print" };
                    var hsActiveResult = await _routerOsProvider.ExecuteAsync(hsCmd);
                    if (hsActiveResult.IsSuccess && hsActiveResult.Value?.RawData != null)
                    {
                        liveActiveHotspot = hsActiveResult.Value.RawData.Count;
                        liveActive += liveActiveHotspot;
                        diagnostics.Add($"مستخدمي Hotspot النشطين من الراوتر: {liveActiveHotspot}");
                    }
                    else
                    {
                        diagnostics.Add($"فشل استعلام Hotspot النشطين: {hsActiveResult.ErrorMessage}");
                    }

                    // 2. PPP active users count
                    var pppCmd = new MikroTikCommand { Command = "/ppp/active/print" };
                    var pppActiveResult = await _routerOsProvider.ExecuteAsync(pppCmd);
                    if (pppActiveResult.IsSuccess && pppActiveResult.Value?.RawData != null)
                    {
                        liveActivePpp = pppActiveResult.Value.RawData.Count;
                        liveActive += liveActivePpp;
                        diagnostics.Add($"مستخدمي PPP النشطين من الراوتر: {liveActivePpp}");
                    }
                    else
                    {
                        diagnostics.Add($"فشل استعلام PPP النشطين: {pppActiveResult.ErrorMessage}");
                    }

                    // 3. Hotspot hosts count
                    var hostCmd = new MikroTikCommand { Command = "/ip/hotspot/host/print" };
                    var hostsResult = await _routerOsProvider.ExecuteAsync(hostCmd);
                    if (hostsResult.IsSuccess && hostsResult.Value?.RawData != null)
                    {
                        liveHostsVal = hostsResult.Value.RawData.Count;
                        diagnostics.Add($"عدد الهوستس (Hosts) المتصلين بالراوتر: {hostsResult.Value.RawData.Count}");
                    }
                    else
                    {
                        diagnostics.Add($"فشل استعلام الهوستس: {hostsResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.Add($"خطأ أثناء استعلام الأرقام الحية: {ex.Message}");
                }
            }
            else
            {
                diagnostics.Add("تنبيه: مزود الاتصال غير متصل، تعذر استعلام الأرقام الحية.");
            }

            await Task.Run(() =>
            {
                int total = 0, online = 0, offline = 0, warning = 0, critical = 0, projects = 0, sessions = 0;

                if (hasUsermanDb)
                {
                    try
                    {
                        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Cache=Shared");
                        conn.Open();

                        using var cmd = conn.CreateCommand();

                        // 1. Total users
                        cmd.CommandText = "SELECT COUNT(*) FROM user";
                        total = Convert.ToInt32(cmd.ExecuteScalar());

                        // 2. Active users (state = 1)
                        cmd.CommandText = "SELECT COUNT(*) FROM userprofile WHERE state = 1 AND paused = 0";
                        online = Convert.ToInt32(cmd.ExecuteScalar());

                        // 3. Expired users (state = 2)
                        cmd.CommandText = "SELECT COUNT(*) FROM userprofile WHERE state = 2";
                        offline = Convert.ToInt32(cmd.ExecuteScalar());

                        // 4. Paused users (paused = 1)
                        cmd.CommandText = "SELECT COUNT(*) FROM userprofile WHERE paused = 1";
                        warning = Convert.ToInt32(cmd.ExecuteScalar());

                        // 5. Unused users (activated = 0)
                        cmd.CommandText = "SELECT COUNT(*) FROM userprofile WHERE activated = 0";
                        critical = Convert.ToInt32(cmd.ExecuteScalar());

                        // 6. Total profiles
                        cmd.CommandText = "SELECT COUNT(*) FROM profile WHERE name <> ''";
                        projects = Convert.ToInt32(cmd.ExecuteScalar());

                        // 7. Active sessions (lastSeenAt > 0)
                        cmd.CommandText = "SELECT COUNT(*) FROM user WHERE lastSeenAt > 0";
                        sessions = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    catch (Exception ex)
                    {
                        diagnostics.Add($"خطأ أثناء قراءة UserManager SQLite: {ex.Message}");
                    }
                }

                _dispatcherService.InvokeAsync(() =>
                {
                    TotalDevices = total;
                    OnlineDevices = online;
                    OfflineDevices = offline;
                    WarningDevices = warning;
                    CriticalDevices = critical;
                    TotalProjects = projects;
                    ActiveMonitoringSessions = sessions;
                    LiveActiveUsers = liveActive;
                    LiveActiveHotspotUsers = liveActiveHotspot;
                    LiveActivePppUsers = liveActivePpp;
                    LiveActiveDetails = $"هوتسبوت: {liveActiveHotspot} | PPP: {liveActivePpp}";
                    LiveHosts = liveHostsVal;

                    RecentAlerts.Clear();
                    foreach (var diag in diagnostics)
                    {
                        RecentAlerts.Add(new DashboardAlertDto
                        {
                            Severity = diag.Contains("فشل") || diag.Contains("خطأ") || diag.Contains("تنبيه") ? "تنبيه" : "معلومات",
                            Message = diag,
                            Timestamp = DateTime.Now
                        });
                    }

                    RecentAlerts.Add(new DashboardAlertDto
                    {
                        Severity = "معلومات",
                        Message = $"تم تحديث إحصائيات UserManager بنجاح من قاعدة البيانات المحلية للراوتر: {router.DisplayName}.",
                        Timestamp = DateTime.Now
                    });

                    if (critical > 0)
                    {
                        RecentAlerts.Add(new DashboardAlertDto
                        {
                            Severity = "تنبيه",
                            Message = $"يوجد عدد {critical} كارت غير مفعل في المخزون. جاهز للطباعة والبيع.",
                            Timestamp = DateTime.Now
                        });
                    }
                });
            });
        }
        catch (Exception ex)
        {
            _dispatcherService.Invoke(() =>
            {
                RecentAlerts.Clear();
                RecentAlerts.Add(new DashboardAlertDto
                {
                    Severity = "خطأ",
                    Message = $"فشل في قراءة قاعدة البيانات المحلية: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            });
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        UpdateInfo();
        await LoadRealDataAsync();
    }

    public override void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        _autoRefreshService.UnregisterCallback(LoadRealDataAsync);
        base.Dispose();
    }
}
