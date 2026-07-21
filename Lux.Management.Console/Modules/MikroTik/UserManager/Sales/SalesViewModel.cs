using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Sales;

public partial class SalesViewModel : ViewModelBase, IActivatable
{
    private readonly ISalesQueryService _salesQueryService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IVoucherBackgroundImportManager _backgroundImportManager;
    private readonly IDispatcherService _dispatcherService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SalesViewModel> _logger;
    private readonly DispatcherTimer _relativeSyncTimer;
    private readonly DispatcherTimer _debounceTimer;

    private CancellationTokenSource? _queryCts;
    private int _isRefreshingQuery; // Thread-safe guard: 0 = false, 1 = true
    private const int DefaultPageSize = 50;

    private long? _lastLoadedActivated;
    private int? _lastLoadedId;

    // ── مجموعات البيانات ─────────────────────────────────────────
    public ObservableCollection<SalesRecordDto> SalesRecords { get; } = new();

    private SalesRecordDto? _selectedRecord;
    public SalesRecordDto? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
            {
                ShowVoucherDetailsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    // ── الإحصائيات (KPIs) ─────────────────────────────────────────
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private int _yesterdayCount;
    [ObservableProperty] private int _weeklyCount;
    [ObservableProperty] private int _monthlyCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _unusedInventory;

    [ObservableProperty] private long _todayRevenue;
    [ObservableProperty] private long _yesterdayRevenue;
    [ObservableProperty] private long _weeklyRevenue;
    [ObservableProperty] private long _monthlyRevenue;
    [ObservableProperty] private long _totalRevenue;

    [ObservableProperty] private string _todayBestProfile = "لا يوجد";
    [ObservableProperty] private string _yesterdayBestProfile = "لا يوجد";
    [ObservableProperty] private string _relativeLastSyncText = "غير متوفر";

    // ── الفلاتر ──────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _filterStatus = string.Empty; // ""=All, "active", "expired", "paused"
    [ObservableProperty] private string _filterProfile = "كل الباقات";
    [ObservableProperty] private DateTime? _selectedDate; // null = كل التواريخ

    public ObservableCollection<string> ProfileFilters { get; } = new() { "كل الباقات" };

    // ── المؤشرات وحالة التحميل ──────────────────────────────────
    [ObservableProperty] private bool _hasMoreItems = true;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _statusBannerMessage = "البيانات محدثة";
    [ObservableProperty] private bool _isStatusBannerVisible;
    [ObservableProperty] private string _lastSyncTimeText = "—";
    [ObservableProperty] private int _globalCount; // إجمالي النتائج المفلترة

    // ── معلومات الراوتر ──────────────────────────────────────────
    [ObservableProperty] private string _routerName = "لا يوجد راوتر نشط";
    [ObservableProperty] private bool _isRouterConnected;

    // ── الأوامر ──────────────────────────────────────────────────
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand ClearFilterCommand { get; }
    public IRelayCommand SelectTodayCommand { get; }
    public IRelayCommand SelectPreviousDayCommand { get; }
    public IRelayCommand SelectNextDayCommand { get; }
    public IAsyncRelayCommand ShowVoucherDetailsCommand { get; }

    public SalesViewModel(
        ISalesQueryService salesQueryService,
        IActiveRouterContext activeRouterContext,
        IVoucherBackgroundImportManager backgroundImportManager,
        IDispatcherService dispatcherService,
        IPermissionService permissionService,
        IEventBus eventBus,
        INotificationService notificationService,
        ILogger<SalesViewModel> logger) : base(permissionService, eventBus)
    {
        _salesQueryService = salesQueryService;
        _activeRouterContext = activeRouterContext;
        _backgroundImportManager = backgroundImportManager;
        _dispatcherService = dispatcherService;
        _notificationService = notificationService;
        _logger = logger;

        Title = "المبيعات والنشاط";

        // تهيئة الأوامر
        LoadCommand = new AsyncRelayCommand(InitializeAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshCommandExecuteAsync);
        ClearFilterCommand = new RelayCommand(ClearFilters);
        ShowVoucherDetailsCommand = new AsyncRelayCommand(ShowVoucherDetailsAsync, () => SelectedRecord != null);

        SelectTodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today);
        SelectPreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate?.AddDays(-1) ?? DateTime.Today.AddDays(-1));
        SelectNextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate?.AddDays(1) ?? DateTime.Today.AddDays(1));

        // تهيئة Debounce Timer للبحث
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;

        // تهيئة مؤقت تحديث الوقت النسبي للمزامنة
        _relativeSyncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _relativeSyncTimer.Tick += (s, e) => UpdateRelativeLastSyncText();
        _relativeSyncTimer.Start();
    }

    public Task ActivateAsync() => InitializeAsync();

    public async Task InitializeAsync()
    {
        RouterName = _activeRouterContext.CurrentRouter?.DisplayName ?? "لا يوجد راوتر نشط";
        IsRouterConnected = _activeRouterContext.CurrentRouter != null;

        // نقوم بتحميل جميع التواريخ افتراضياً لكي لا تظهر الشاشة فارغة عند عدم وجود مبيعات اليوم
        await RefreshCurrentQueryAsync();
    }

    // ── تغيير الفلاتر يحفز إعادة التحميل تلقائياً ──────────────────────
    partial void OnSearchTextChanged(string value)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    partial void OnFilterStatusChanged(string value)
    {
        _ = RefreshCurrentQueryAsync();
    }

    partial void OnFilterProfileChanged(string value)
    {
        _ = RefreshCurrentQueryAsync();
    }

    partial void OnSelectedDateChanged(DateTime? value)
    {
        _ = RefreshCurrentQueryAsync();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _ = RefreshCurrentQueryAsync();
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        FilterStatus = string.Empty;
        FilterProfile = "كل الباقات";
        SelectedDate = null;

        _ = RefreshCurrentQueryAsync();
    }

    // ── جلب البيانات الرئيسي ───────────────────────────────────────
    public async Task RefreshCurrentQueryAsync()
    {
        if (Interlocked.CompareExchange(ref _isRefreshingQuery, 1, 0) != 0) return;

        // إلغاء أي عملية سابقة
        _queryCts?.Cancel();
        _queryCts?.Dispose();
        _queryCts = new CancellationTokenSource();
        var token = _queryCts.Token;

        IsRefreshing = true;
        ShowStatusBanner("جاري تحميل بيانات المبيعات...");

        try
        {
            var routerId = _activeRouterContext.CurrentRouterId;
            if (routerId == null)
            {
                await _dispatcherService.InvokeAsync(SalesRecords.Clear);
                GlobalCount = 0;
                HideStatusBanner();
                return;
            }

            // 1. مزامنة قاعدة البيانات من الراوتر إذا كانت غير موجودة أو تم طلب التحديث يدوياً
            var dbPath = _backgroundImportManager.GetCachedCleanDbPath(routerId.Value);

            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath) || _forceDownload)
            {
                ShowStatusBanner("جاري تحميل قاعدة مبيعات User Manager من الراوتر...");
                try
                {
                    await Task.Run(async () =>
                    {
                        await _backgroundImportManager.DownloadAndCacheDbAsync(routerId.Value, token);
                    }, token);
                    dbPath = _backgroundImportManager.GetCachedCleanDbPath(routerId.Value);
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "⚠️ فشل تحميل قاعدة البيانات من الراوتر. محاولة استخدام آخر كاش متاح...");
                }
            }

            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                ShowStatusBanner("⚠️ لم يتم العثور على قاعدة بيانات الراوتر. الرجاء الضغط على 'تحديث البيانات' للمزامنة.");
                await _dispatcherService.InvokeAsync(SalesRecords.Clear);
                GlobalCount = 0;
                return;
            }

            var parameters = new SalesQueryParameters
            {
                RouterDbPath = dbPath,
                FilterDate = SelectedDate.HasValue ? DateOnly.FromDateTime(SelectedDate.Value) : null,
                SearchText = SearchText,
                FilterStatus = FilterStatus,
                FilterProfile = FilterProfile,
                PageSize = DefaultPageSize
            };

            // 1. جلب السجلات بـ Keyset Pagination
            var result = await _salesQueryService.GetSalesKeysetAsync(parameters, token);

            // 2. تحديث الـ KPIs
            var kpis = await _salesQueryService.GetSalesKpiAsync(parameters, token);
            TodayCount = kpis.TodaySales;
            YesterdayCount = kpis.YesterdaySales;
            WeeklyCount = kpis.WeeklySales;
            MonthlyCount = kpis.MonthlySales;
            TotalCount = kpis.TotalSales;
            UnusedInventory = kpis.UnusedInventory;

            TodayRevenue = kpis.TodayRevenue;
            YesterdayRevenue = kpis.YesterdayRevenue;
            WeeklyRevenue = kpis.WeeklyRevenue;
            MonthlyRevenue = kpis.MonthlyRevenue;
            TotalRevenue = kpis.TotalRevenue;

            TodayBestProfile = kpis.TodayBestProfile;
            YesterdayBestProfile = kpis.YesterdayBestProfile;

            UpdateRelativeLastSyncText();

            GlobalCount = result.TotalCount;

            // 3. تحديث القائمة في الواجهة
            await _dispatcherService.InvokeAsync(() =>
            {
                SalesRecords.Clear();
                foreach (var record in result.Items)
                {
                    SalesRecords.Add(record);
                }
            });

            // 4. تعيين مؤشرات Keyset
            if (result.Items.Count > 0)
            {
                var last = result.Items[^1];
                _lastLoadedActivated = last.ActivatedUnix;
                _lastLoadedId = last.Id;
                HasMoreItems = result.Items.Count == DefaultPageSize;
            }
            else
            {
                _lastLoadedActivated = null;
                _lastLoadedId = null;
                HasMoreItems = false;
            }

            // 6. تحميل الباقات في الفلتر إذا لم تكن محملة بعد
            if (_forceDownload || ProfileFilters.Count <= 1)
            {
                var profiles = await _salesQueryService.GetProfilesAsync(dbPath, token);
                await _dispatcherService.InvokeAsync(() =>
                {
                    var currentSelected = FilterProfile;
                    ProfileFilters.Clear();
                    ProfileFilters.Add("كل الباقات");
                    foreach (var p in profiles)
                    {
                        ProfileFilters.Add(p);
                    }
                    // استعادة التحديد القديم إن وجد
                    if (ProfileFilters.Contains(currentSelected))
                        FilterProfile = currentSelected;
                    else
                        FilterProfile = "كل الباقات";
                });
            }

            HideStatusBanner();
        }
        catch (OperationCanceledException)
        {
            // عملية متوقعة عند الإلغاء
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل جلب سجلات المبيعات");
            ShowStatusBanner($"⚠️ فشل تحميل البيانات: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isRefreshingQuery, 0);
            IsRefreshing = false;
            LastSyncTimeText = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }
    }

    private bool _forceDownload;

    private async Task RefreshCommandExecuteAsync()
    {
        _forceDownload = true;
        try
        {
            await RefreshCurrentQueryAsync();
        }
        finally
        {
            _forceDownload = false;
        }
    }

    // ── تحميل المزيد (Infinite Scrolling) ────────────────────────
    public async Task LoadNextPageAsync()
    {
        if (!HasMoreItems || IsLoadingMore) return;
        IsLoadingMore = true;

        var token = _queryCts?.Token ?? CancellationToken.None;

        try
        {
            var routerId = _activeRouterContext.CurrentRouterId;
            if (routerId == null) return;

            var dbPath = _backgroundImportManager.GetCachedCleanDbPath(routerId.Value);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                return;
            }

            var parameters = new SalesQueryParameters
            {
                RouterDbPath = dbPath,
                FilterDate = SelectedDate.HasValue ? DateOnly.FromDateTime(SelectedDate.Value) : null,
                SearchText = SearchText,
                FilterStatus = FilterStatus,
                FilterProfile = FilterProfile,
                AfterActivated = _lastLoadedActivated,
                AfterId = _lastLoadedId,
                PageSize = DefaultPageSize
            };

            var result = await _salesQueryService.GetSalesKeysetAsync(parameters, token);

            if (result.Items.Count > 0)
            {
                await _dispatcherService.InvokeAsync(() =>
                {
                    foreach (var record in result.Items)
                    {
                        SalesRecords.Add(record);
                    }
                });

                var last = result.Items[^1];
                _lastLoadedActivated = last.ActivatedUnix;
                _lastLoadedId = last.Id;
                HasMoreItems = result.Items.Count == DefaultPageSize;
            }
            else
            {
                HasMoreItems = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل الصفحة التالية من المبيعات");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    // ── شريط الحالة ──────────────────────────────────────────────
    private void ShowStatusBanner(string message)
    {
        StatusBannerMessage = message;
        IsStatusBannerVisible = true;
    }

    private void HideStatusBanner()
    {
        IsStatusBannerVisible = false;
    }

    private void UpdateRelativeLastSyncText()
    {
        try
        {
            var routerId = _activeRouterContext.CurrentRouterId;
            if (routerId == null)
            {
                RelativeLastSyncText = "غير متوفر";
                return;
            }

            var dbPath = _backgroundImportManager.GetCachedCleanDbPath(routerId.Value);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                RelativeLastSyncText = "غير متوفر (لم تتم المزامنة)";
                return;
            }

            var lastWrite = File.GetLastWriteTime(dbPath);
            var span = DateTime.Now - lastWrite;

            if (span.TotalSeconds < 15)
            {
                RelativeLastSyncText = "الآن";
            }
            else if (span.TotalSeconds < 60)
            {
                RelativeLastSyncText = $"منذ {Math.Round(span.TotalSeconds)} ثانية";
            }
            else if (span.TotalMinutes < 60)
            {
                RelativeLastSyncText = $"منذ {Math.Round(span.TotalMinutes)} دقيقة";
            }
            else if (span.TotalHours < 24)
            {
                RelativeLastSyncText = $"منذ {Math.Round(span.TotalHours)} ساعة";
            }
            else
            {
                RelativeLastSyncText = $"منذ {Math.Round(span.TotalDays)} يوم";
            }
        }
        catch
        {
            RelativeLastSyncText = "غير متوفر";
        }
    }

    private async Task ShowVoucherDetailsAsync()
    {
        if (SelectedRecord == null || _activeRouterContext.CurrentRouter == null) return;
        var routerId = _activeRouterContext.CurrentRouter.Id;
        var username = SelectedRecord.VoucherCode;
        
        await ExecuteBusyAsync(async (token) =>
        {
            var dbPath = _backgroundImportManager.GetCachedCleanDbPath(routerId);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                await _backgroundImportManager.DownloadAndCacheDbAsync(routerId, token);
                dbPath = _backgroundImportManager.GetCachedCleanDbPath(routerId);
            }

            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                await _dispatcherService.InvokeAsync(() =>
                {
                    _notificationService.ShowWarning("لم يتم العثور على قاعدة بيانات اليوزر منجر للراوتر الحالي. الرجاء المزامنة أولاً.");
                });
                return;
            }

            Dictionary<string, string>? leases = null;
            try
            {
                leases = await _backgroundImportManager.GetDhcpLeasesAsync(routerId, token);
            }
            catch { }

            await _dispatcherService.InvokeAsync(() =>
            {
                var routerName = _activeRouterContext.CurrentRouter?.DisplayName ?? "—";
                var window = new Vouchers.Views.UserReportWindow(username, dbPath, routerName, leases);
                window.Owner = System.Windows.Application.Current.MainWindow;
                window.ShowDialog();
            });
        }, "جاري جلب تفاصيل الجلسات والبيانات...");
    }
}
