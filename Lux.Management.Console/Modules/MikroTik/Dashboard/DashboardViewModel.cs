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
    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<DashboardAlertDto> _recentAlerts = new();

    public DashboardViewModel(
        IPermissionService permissionService,
        IEventBus eventBus,
        IActiveRouterContext activeRouterContext,
        IDispatcherService dispatcherService,
        IAutoRefreshService autoRefreshService,
        IVoucherBackgroundImportManager backgroundImportManager) 
        : base(permissionService, eventBus)
    {
        _activeRouterContext = activeRouterContext;
        _dispatcherService = dispatcherService;
        _autoRefreshService = autoRefreshService;
        _backgroundImportManager = backgroundImportManager;

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
            _dispatcherService.Invoke(() => RecentAlerts.Clear());
            return;
        }

        var dbPath = _backgroundImportManager.GetCachedCleanDbPath(router.Id);
        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            TotalDevices = 0;
            OnlineDevices = 0;
            OfflineDevices = 0;
            WarningDevices = 0;
            CriticalDevices = 0;
            TotalProjects = 0;
            ActiveMonitoringSessions = 0;
            _dispatcherService.Invoke(() =>
            {
                RecentAlerts.Clear();
                RecentAlerts.Add(new DashboardAlertDto
                {
                    Severity = "تنبيه",
                    Message = "قاعدة بيانات UserManager غير متوفرة محلياً. يرجى الانتظار للمزامنة التلقائية أو الضغط على تحديث البيانات.",
                    Timestamp = DateTime.Now
                });
            });
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Cache=Shared");
                conn.Open();

                using var cmd = conn.CreateCommand();

                // 1. Total users
                cmd.CommandText = "SELECT COUNT(*) FROM user";
                var total = Convert.ToInt32(cmd.ExecuteScalar());

                // 2. Active users (state = 1)
                cmd.CommandText = "SELECT COUNT(*) FROM userprofile WHERE state = 1 AND paused = 0";
                var online = Convert.ToInt32(cmd.ExecuteScalar());

                // 3. Expired users (state = 2)
                cmd.CommandText = "SELECT COUNT(*) FROM userprofile WHERE state = 2";
                var offline = Convert.ToInt32(cmd.ExecuteScalar());

                // 4. Paused users (paused = 1)
                cmd.CommandText = "SELECT COUNT(*) FROM userprofile WHERE paused = 1";
                var warning = Convert.ToInt32(cmd.ExecuteScalar());

                // 5. Unused users (activated = 0)
                cmd.CommandText = "SELECT COUNT(*) FROM userprofile WHERE activated = 0";
                var critical = Convert.ToInt32(cmd.ExecuteScalar());

                // 6. Total profiles
                cmd.CommandText = "SELECT COUNT(*) FROM profile WHERE name <> ''";
                var projects = Convert.ToInt32(cmd.ExecuteScalar());

                // 7. Active sessions (lastSeenAt > 0)
                cmd.CommandText = "SELECT COUNT(*) FROM user WHERE lastSeenAt > 0";
                var sessions = Convert.ToInt32(cmd.ExecuteScalar());

                _dispatcherService.InvokeAsync(() =>
                {
                    TotalDevices = total;
                    OnlineDevices = online;
                    OfflineDevices = offline;
                    WarningDevices = warning;
                    CriticalDevices = critical;
                    TotalProjects = projects;
                    ActiveMonitoringSessions = sessions;

                    RecentAlerts.Clear();
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
