using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Services;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using System.Windows.Threading;
using MikroTikVoucherPrinter.Application.State;
using Microsoft.EntityFrameworkCore;
using Lux.Management.Console.Services;
using Lux.Management.Console.Core.Security.Authorization;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Security.Configuration;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.Views;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;

public enum ScreenStatus
{
    Loading,
    Connecting,
    Syncing,
    Offline,
    PendingChanges,
    Updated,
    Failed,
    ImportingLegacyVouchers
}

public class AgentFilterItem
{
    public Guid Id { get; }
    public string Name { get; }
    public AgentFilterItem(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}

public partial class VoucherManagementViewModel : ViewModelBase, IActivatable
{
    private readonly IVoucherQueryService _queryService;
    private readonly ISyncService _syncService;
    private readonly IPrintService _printService;
    private readonly IPrintPreviewService _printPreviewService;
    private readonly IVoucherManagementService _managementService;
    private readonly ISettingsService _settingsService;
    public ISettingsService SettingsService => _settingsService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IUserNotificationService _notificationService;
    private readonly IClipboardService _clipboardService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IVoucherPageStateTracker _stateTracker;
    private readonly IVoucherBackgroundImportManager _backgroundImportManager;
    private readonly IDbContextFactory<MikroTikVoucherPrinter.Infrastructure.Data.LuxCardDbContext> _dbFactory;
    private readonly ITemplateService _templateService;
    private readonly IShellState _shellState;
    private readonly IFeatureAuthorizationService _featureAuthorizationService;
    private readonly IRouterManagementService _routerService;
    private readonly IProfileService _profileService;
    private readonly ILogger<VoucherManagementViewModel> _logger;

    // خصائص معالج الاستيراد
    private bool _isFirstRunImportRequired;
    public bool IsFirstRunImportRequired { get => _isFirstRunImportRequired; set => SetProperty(ref _isFirstRunImportRequired, value); }

    private bool _isImporting;
    public bool IsImporting { get => _isImporting; set => SetProperty(ref _isImporting, value); }

    private int _importProgressPercent;
    public int ImportProgressPercent { get => _importProgressPercent; set => SetProperty(ref _importProgressPercent, value); }

    private int _importedCount;
    public int ImportedCount { get => _importedCount; set => SetProperty(ref _importedCount, value); }

    private int _totalImportCount;
    public int TotalImportCount { get => _totalImportCount; set => SetProperty(ref _totalImportCount, value); }

    private bool _isImportPaused;
    public bool IsImportPaused { get => _isImportPaused; set => SetProperty(ref _isImportPaused, value); }

    private int _routerVoucherCount;
    public int RouterVoucherCount { get => _routerVoucherCount; set => SetProperty(ref _routerVoucherCount, value); }

    private bool _isImportDetailsHidden;
    public bool IsImportDetailsHidden { get => _isImportDetailsHidden; set => SetProperty(ref _isImportDetailsHidden, value); }

    // أوامر معالج الاستيراد
    public IRelayCommand DismissImportCommand { get; }
    public IRelayCommand PauseImportCommand { get; }
    public IRelayCommand ResumeImportCommand { get; }
    public IRelayCommand HideImportDetailsCommand { get; }
    public IAsyncRelayCommand StartBackgroundImportCommand { get; }
    public IAsyncRelayCommand<VoucherDto> ToggleFavoriteCommand { get; }

    // إلغاء الاستعلامات الجارية
    private CancellationTokenSource? _queryCts;
    private DispatcherTimer? _debounceTimer;

    // ══════════════════════════════════════════════════════
    //  مراقب المزامنة الدوري (Sync Watchdog)
    //  يفحص كل 5 ثوانٍ هل يوجد كروت معلقة + اتصال جاهز
    // ══════════════════════════════════════════════════════
    private System.Threading.Timer? _syncWatchdogTimer;
    private bool _watchdogStarted = false;

    // ══════════════════════════════════════════════════════
    //  البيانات والجدول
    // ══════════════════════════════════════════════════════
    public ObservableCollection<VoucherDto> Vouchers { get; } = new();
    public HashSet<Guid> SelectedVoucherIds => _stateTracker.SelectedVoucherIds;

    // ══════════════════════════════════════════════════════
    //  شجرة الملاحة الجانبية (Sidebar Tree)
    // ══════════════════════════════════════════════════════
    public ObservableCollection<NavigationNodeDto> NavigationTree { get; } = new();
    
    private NavigationNodeDto? _selectedNode;
    public NavigationNodeDto? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                OnPropertyChanged(nameof(IsRecycleBinActive));
                OnPropertyChanged(nameof(IsNotRecycleBinActive));
                if (value != null)
                {
                    _stateTracker.SelectedNodeId = value.Id;
                    _stateTracker.SelectedNodeCategory = value.Category;
                    _stateTracker.SelectedNodeValue = value.AssociatedValue;
                    FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════
    //  البحث الموحد والعدادات المفلترة
    // ══════════════════════════════════════════════════════
    public string SearchText
    {
        get => _stateTracker.SearchText;
        set
        {
            if (_stateTracker.SearchText != value)
            {
                _stateTracker.SearchText = value;
                OnPropertyChanged(nameof(SearchText));
                
                // تشغيل البحث مع Debounce 300ms
                TriggerDebouncedSearch();
            }
        }
    }

    public bool IsExactSearch
    {
        get => _stateTracker.IsExactSearch;
        set
        {
            if (_stateTracker.IsExactSearch != value)
            {
                _stateTracker.IsExactSearch = value;
                OnPropertyChanged(nameof(IsExactSearch));
                FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
            }
        }
    }

    private int _globalCount;
    public int GlobalCount
    {
        get => _globalCount;
        set => SetProperty(ref _globalCount, value);
    }

    // ══════════════════════════════════════════════════════
    //  الفلاتر العامة المساعدة
    // ══════════════════════════════════════════════════════
    public string FilterStatus
    {
        get => _stateTracker.FilterStatus;
        set
        {
            if (_stateTracker.FilterStatus != value)
            {
                _stateTracker.FilterStatus = value;
                OnPropertyChanged(nameof(FilterStatus));

                if (value == "Deleted")
                {
                    _stateTracker.SelectedNodeCategory = "recyclebin";
                    _stateTracker.SelectedNodeValue = string.Empty;
                }
                else
                {
                    _stateTracker.SelectedNodeCategory = "all";
                    _stateTracker.SelectedNodeValue = string.Empty;
                }

                OnPropertyChanged(nameof(IsRecycleBinActive));
                OnPropertyChanged(nameof(IsNotRecycleBinActive));
                FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
            }
        }
    }

    public string FilterSync
    {
        get => _stateTracker.FilterSync;
        set
        {
            if (_stateTracker.FilterSync != value)
            {
                _stateTracker.FilterSync = value;
                OnPropertyChanged(nameof(FilterSync));
                FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
            }
        }
    }

    public string FilterProfile
    {
        get => _stateTracker.FilterProfile;
        set
        {
            if (_stateTracker.FilterProfile != value)
            {
                _stateTracker.FilterProfile = value;
                OnPropertyChanged(nameof(FilterProfile));
                FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
            }
        }
    }

    public ObservableCollection<string> ProfileFilters { get; } = new() { "كل الباقات" };

    private string _selectedQuickFilter = "all";
    public string SelectedQuickFilter
    {
        get => _selectedQuickFilter;
        set
        {
            if (SetProperty(ref _selectedQuickFilter, value))
            {
                if (value == "deleted")
                {
                    _stateTracker.SelectedNodeCategory = "recyclebin";
                    _stateTracker.SelectedNodeValue = string.Empty;
                    FilterStatus = "All";
                }
                else
                {
                    _stateTracker.SelectedNodeCategory = "all";
                    _stateTracker.SelectedNodeValue = string.Empty;

                    if (value == "unused")
                        FilterStatus = "Unused";
                    else if (value == "used" || value == "active" || value == "unused_active")
                        FilterStatus = "Used";
                    else if (value == "expired")
                        FilterStatus = "Expired";
                    else
                        FilterStatus = "All";
                }
                OnPropertyChanged(nameof(IsRecycleBinActive));
                OnPropertyChanged(nameof(IsNotRecycleBinActive));
                FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
            }
        }
    }

    private Guid? _filterAgentId = Guid.Empty;
    public Guid? FilterAgentId
    {
        get => _filterAgentId ?? Guid.Empty;
        set
        {
            var targetValue = value ?? Guid.Empty;
            if (SetProperty(ref _filterAgentId, targetValue))
            {
                if (targetValue == Guid.Empty)
                {
                    _stateTracker.SelectedNodeCategory = "all";
                    _stateTracker.SelectedNodeValue = string.Empty;
                }
                else
                {
                    _stateTracker.SelectedNodeCategory = "agents";
                    _stateTracker.SelectedNodeValue = targetValue.ToString();
                }
                FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
            }
        }
    }

    public ObservableCollection<AgentFilterItem> AgentFilters { get; } = new() { new AgentFilterItem(Guid.Empty, "كل الوكلاء") };

    // ══════════════════════════════════════════════════════
    //  مكون حالة الراوتر الثابت (Router Status Widget)
    // ══════════════════════════════════════════════════════
    private string _routerName = "لا يوجد راوتر نشط";
    public string RouterName { get => _routerName; set => SetProperty(ref _routerName, value); }

    private bool _isRouterConnected;
    public bool IsRouterConnected { get => _isRouterConnected; set => SetProperty(ref _isRouterConnected, value); }

    private string _lastSyncTimeText = "—";
    public string LastSyncTimeText { get => _lastSyncTimeText; set => SetProperty(ref _lastSyncTimeText, value); }

    private int _pendingSyncCount;
    public int PendingSyncCount { get => _pendingSyncCount; set => SetProperty(ref _pendingSyncCount, value); }

    private int _failedSyncCount;
    public int FailedSyncCount
    {
        get => _failedSyncCount;
        set
        {
            if (SetProperty(ref _failedSyncCount, value))
            {
                OnPropertyChanged(nameof(HasFailedSyncs));
            }
        }
    }

    public bool IsRecycleBinActive => SelectedQuickFilter == "deleted" || _stateTracker.SelectedNodeCategory?.ToLowerInvariant() == "recyclebin";
    public bool IsNotRecycleBinActive => !IsRecycleBinActive;
    public bool HasFailedSyncs => FailedSyncCount > 0;

    // ══════════════════════════════════════════════════════
    //  شريط الحالة المنزلق (Status Banner System)
    // ══════════════════════════════════════════════════════
    private ScreenStatus _currentScreenStatus = ScreenStatus.Updated;
    public ScreenStatus CurrentScreenStatus { get => _currentScreenStatus; set => SetProperty(ref _currentScreenStatus, value); }

    private string _statusBannerMessage = "البيانات محدثة";
    public string StatusBannerMessage { get => _statusBannerMessage; set => SetProperty(ref _statusBannerMessage, value); }

    private bool _isStatusBannerVisible;
    public bool IsStatusBannerVisible { get => _isStatusBannerVisible; set => SetProperty(ref _isStatusBannerVisible, value); }

    private int _syncProcessedCount;
    public int SyncProcessedCount { get => _syncProcessedCount; set => SetProperty(ref _syncProcessedCount, value); }

    private int _syncTotalCount;
    public int SyncTotalCount { get => _syncTotalCount; set => SetProperty(ref _syncTotalCount, value); }

    private double _syncProgressPercent;
    public double SyncProgressPercent { get => _syncProgressPercent; set => SetProperty(ref _syncProgressPercent, value); }

    private bool _isSyncingWithRouter;
    public bool IsSyncingWithRouter
    {
        get => _isSyncingWithRouter;
        set
        {
            if (SetProperty(ref _isSyncingWithRouter, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    // ══════════════════════════════════════════════════════
    //  الأوامر المعتمدة
    // ══════════════════════════════════════════════════════
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RetryFailedCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand RestoreSelectedCommand { get; }
    public IAsyncRelayCommand PrintSelectedCommand { get; }
    public IRelayCommand ClearFilterCommand { get; }

    // Stub Commands — مربوطة في XAML وتحتاج تنفيذاً
    public IAsyncRelayCommand CreateBatchCommand { get; }
    public IAsyncRelayCommand ExportSelectedCommand { get; }

    public IAsyncRelayCommand<VoucherDto> ShowSessionsCommand { get; }
    public IAsyncRelayCommand<VoucherDto> ShowVoucherDetailsCommand { get; }
    public IRelayCommand<VoucherDto> CopyUsernameCommand { get; }
    public IRelayCommand<VoucherDto> CopyPasswordCommand { get; }

    private VoucherDto? _selectedVoucher;
    public VoucherDto? SelectedVoucher
    {
        get => _selectedVoucher;
        set
        {
            if (SetProperty(ref _selectedVoucher, value))
            {
                ShowVoucherDetailsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    // لتحديد الأعداد محلياً من شاشة code-behind
    private int _selectedCount;
    public int SelectedCount
    {
        get => _selectedCount;
        set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                DeleteSelectedCommand.NotifyCanExecuteChanged();
                RestoreSelectedCommand.NotifyCanExecuteChanged();
                PrintSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _isAllSelected;
    public bool IsAllSelected
    {
        get => _isAllSelected;
        set => SetProperty(ref _isAllSelected, value);
    }


    // ══════════════════════════════════════════════════════
    //  المنشئ (Constructor)
    // ══════════════════════════════════════════════════════
    public VoucherManagementViewModel(
        IVoucherQueryService queryService,
        ISyncService syncService,
        IPrintService printService,
        IPrintPreviewService printPreviewService,
        IVoucherManagementService managementService,
        ISettingsService settingsService,
        IActiveRouterContext activeRouterContext,
        IUserNotificationService notificationService,
        IClipboardService clipboardService,
        IDispatcherService dispatcherService,
        IVoucherPageStateTracker stateTracker,
        IVoucherBackgroundImportManager backgroundImportManager,
        IDbContextFactory<MikroTikVoucherPrinter.Infrastructure.Data.LuxCardDbContext> dbFactory,
        ITemplateService templateService,
        IShellState shellState,
        IPermissionService permissionService,
        IEventBus eventBus,
        IRouterManagementService routerService,
        ILogger<VoucherManagementViewModel> logger,
        IFeatureAuthorizationService featureAuthorizationService,
        IProfileService profileService) : base(permissionService, eventBus)
    {
        _queryService = queryService;
        _syncService = syncService;
        _printService = printService;
        _printPreviewService = printPreviewService;
        _managementService = managementService;
        _settingsService = settingsService;
        _activeRouterContext = activeRouterContext;
        _notificationService = notificationService;
        _clipboardService = clipboardService;
        _dispatcherService = dispatcherService;
        _stateTracker = stateTracker;
        _backgroundImportManager = backgroundImportManager;
        _dbFactory = dbFactory;
        _templateService = templateService;
        _shellState = shellState;
        _featureAuthorizationService = featureAuthorizationService;
        _routerService = routerService;
        _logger = logger;
        _profileService = profileService;

        Title = "إدارة الكروت";

        // ربط الأوامر
        LoadCommand = new AsyncRelayCommand(InitializeAsync);
        RefreshCommand = new AsyncRelayCommand(InstantSyncAsync, () => !IsSyncingWithRouter);
        RetryFailedCommand = new AsyncRelayCommand(RetryFailedDataAsync);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedCount > 0);
        RestoreSelectedCommand = new AsyncRelayCommand(RestoreSelectedAsync, () => SelectedCount > 0);
        PrintSelectedCommand = new AsyncRelayCommand(() => PrintSelectedAsync(), () => SelectedCount > 0);
        ClearFilterCommand = new RelayCommand(ClearFilters);

        // Stub Commands — تجنب Broken Bindings في XAML
        CreateBatchCommand = new AsyncRelayCommand(CreateBatchAsync);
        ExportSelectedCommand = new AsyncRelayCommand(ExportSelectedAsync);

        DismissImportCommand = new RelayCommand(DismissImport);
        PauseImportCommand = new RelayCommand(PauseImport);
        ResumeImportCommand = new RelayCommand(ResumeImport);
        HideImportDetailsCommand = new RelayCommand(HideImportDetails);
        StartBackgroundImportCommand = new AsyncRelayCommand(StartBackgroundImportAsync);

        // الاشتراك في أحداث الاستيراد بالخلفية
        _backgroundImportManager.ProgressChanged += OnProgressChanged;
        _backgroundImportManager.ImportCompleted += OnImportCompleted;
        _backgroundImportManager.ImportError += OnImportError;

        _eventBus.Subscribe<AutoRefreshTriggeredEvent>(this, OnAutoRefreshTriggered);

        ShowSessionsCommand = new AsyncRelayCommand<VoucherDto>(ShowSessionsForVoucherAsync, v => v != null);
        ShowVoucherDetailsCommand = new AsyncRelayCommand<VoucherDto>(ShowVoucherDetailsAsync, v => v != null);
        CopyUsernameCommand = new RelayCommand<VoucherDto>(CopyUsername, v => v != null && !string.IsNullOrEmpty(v.Username));
        CopyPasswordCommand = new RelayCommand<VoucherDto>(CopyPassword, v => v != null);
        ToggleFavoriteCommand = new AsyncRelayCommand<VoucherDto>(ToggleFavoriteAsync);

        // تهيئة المؤقت للـ Search Debouncing
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;

        // تفعيل استرداد الحالة عند الانتقال
        _stateTracker.HasSavedState = true;

        // [FIX Router Switch] تحديث تلقائي للكروت والشجرة عند تغيير الراوتر النشط من الشريط العلوي
        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;

        // [WATCHDOG] بدء المراقب الدوري المضمون
        StartSyncWatchdog();
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            var state = _activeRouterContext.State;

            // [FIX-B] فقط عند الانقطاع الفعلي (وليس خلال Connecting أو Switching)
            if (state == MikroTikVoucherPrinter.Domain.Enums.Platform.ConnectionState.Disconnected
                || state == MikroTikVoucherPrinter.Domain.Enums.Platform.ConnectionState.Error
                || state == MikroTikVoucherPrinter.Domain.Enums.Platform.ConnectionState.AuthenticationFailed
                || state == MikroTikVoucherPrinter.Domain.Enums.Platform.ConnectionState.Timeout)
            {
                // الاتصال انقطع فعلياً → بانر تحذيري فوري
                ShowStatusBanner(ScreenStatus.Offline,
                    "⚠️ انقطع الاتصال بالراوتر. الكروت المعلقة ستُزامَن عند عودة الاتصال.");
                IsRouterConnected = false;
                RouterName = "غير متصل";
                return;
            }

            // [FIX-C3] عند الاتصال الناجح فقط → تهيئة كاملة + مزامنة تلقائية للكروت المعلقة
            if (state == MikroTikVoucherPrinter.Domain.Enums.Platform.ConnectionState.Connected
                && _activeRouterContext.IsConnected)
            {
                await InitializeAsync();
                // TriggerBackgroundSync يُستدعى من داخل InitializeAsync تلقائياً
            }
            // Connecting/Switching → لا نفعل شيئاً، ننتظر حتى يكتمل الاتصال
        });
    }

    // ══════════════════════════════════════════════════════
    //  تهيئة الشاشة والملاحة (Initialization & Sidebar Tree)
    // ══════════════════════════════════════════════════════

    // [PHASE-2] IActivatable.ActivateAsync — يُستدعى عند التنقل الفعلي (Lazy Loading)
    public Task ActivateAsync() => InitializeAsync();

    public async Task InitializeAsync()
    {
        var host = _activeRouterContext.CurrentRouter?.Host ?? "—";
        RouterName = _activeRouterContext.CurrentRouter?.DisplayName ?? "لا يوجد راوتر نشط";
        IsRouterConnected = _activeRouterContext.CurrentRouter != null;

        // 1. بناء شجرة الملاحة الجانبية (Sidebar Tree Nodes)
        BuildNavigationTree();

        // 2. تحديث الـ Widget الثابتة بالإعدادات المعلقة
        await UpdateRouterStatusWidgetAsync();

        // 3. التحقق من كشف التشغيل الأول للراوتر والاستيراد أو حالة الاستيراد النشطة
        if (IsRouterConnected)
        {
            try
            {
                var routerId = _activeRouterContext.CurrentRouter!.Id;
                if (_backgroundImportManager.IsImporting(routerId))
                {
                    IsFirstRunImportRequired = false;
                    IsImporting = true;
                    ImportedCount = _backgroundImportManager.GetImportedCount(routerId);
                    TotalImportCount = _backgroundImportManager.GetTotalImportCount(routerId);
                    ImportProgressPercent = _backgroundImportManager.GetProgressPercent(routerId);
                    IsImportPaused = _backgroundImportManager.IsPaused(routerId);
                    ShowStatusBanner(ScreenStatus.ImportingLegacyVouchers, "جاري استيراد الكروت بالخلفية...");
                }
                else
                {
                    IsFirstRunImportRequired = await _backgroundImportManager.IsImportRequiredAsync(routerId);
                    if (IsFirstRunImportRequired)
                    {
                        RouterVoucherCount = await _backgroundImportManager.GetRouterVoucherCountAsync(routerId);
                    }
                }
            }
            catch
            {
                IsFirstRunImportRequired = false;
                IsImporting = false;
            }
        }

        // 4. استرجاع الفلاتر المحفوظة وتعبئة الواجهة
        if (_stateTracker.HasSavedState)
        {
            // محاولة إيجاد وتحديد العقدة المحفوظة في الشجرة
            var savedNode = FindNodeInTree(NavigationTree, _stateTracker.SelectedNodeId);
            if (savedNode != null)
            {
                _selectedNode = savedNode;
                OnPropertyChanged(nameof(SelectedNode));
                OnPropertyChanged(nameof(IsRecycleBinActive));
                OnPropertyChanged(nameof(IsNotRecycleBinActive));
            }
        }

        // 5. تنفيذ أول استعلام
        await RefreshCurrentQueryAsync();

        // 6. تشغيل المزامنة بالخلفية تلقائياً
        TriggerBackgroundSync();
    }

    private void BuildNavigationTree()
    {
        NavigationTree.Clear();

        // أ. العقد الرئيسية الثابتة
        NavigationTree.Add(new NavigationNodeDto("all", "كل الكروت", "📋", "all"));
        NavigationTree.Add(new NavigationNodeDto("unassigned", "كروت غير مصنفة / يدوية", "📁", "unassigned"));
        NavigationTree.Add(new NavigationNodeDto("imported", "كروت مستوردة مسبقاً", "📥", "imported"));
        NavigationTree.Add(new NavigationNodeDto("recyclebin", "سلة المحذوفات 🗑️", "🗑️", "recyclebin"));

        // ب. مجلد الدفعات (Batches Folder)
        var batchesFolder = new NavigationNodeDto("batches_folder", "الدفعات", "📁", "folder");
        batchesFolder.Children.Add(new NavigationNodeDto("batch:today", "اليوم", "📅", "batches", "today"));
        batchesFolder.Children.Add(new NavigationNodeDto("batch:week", "هذا الأسبوع", "📅", "batches", "week"));
        batchesFolder.Children.Add(new NavigationNodeDto("batch:month", "هذا الشهر", "📅", "batches", "month"));
        batchesFolder.Children.Add(new NavigationNodeDto("batch:recent", "آخر 100 دفعة", "📦", "batches", "recent"));
        batchesFolder.Children.Add(new NavigationNodeDto("batch:search", "بحث عن دفعة...", "🔍", "batches", "search"));
        NavigationTree.Add(batchesFolder);

        // ج. مجلد الوكلاء (Agents Folder with Lazy Loading Dummy)
        var agentsFolder = new NavigationNodeDto("agents_folder", "الوكلاء", "👤", "folder");
        agentsFolder.Children.Add(new NavigationNodeDto("dummy_agent", "تحميل الوكلاء...", "", "dummy") { IsLazyLoadDummy = true });
        NavigationTree.Add(agentsFolder);

        // د. مجلد الباقات (Profiles Folder with Lazy Loading Dummy)
        var profilesFolder = new NavigationNodeDto("profiles_folder", "الباقات", "🏷️", "folder");
        profilesFolder.Children.Add(new NavigationNodeDto("dummy_profile", "تحميل الباقات...", "", "dummy") { IsLazyLoadDummy = true });
        NavigationTree.Add(profilesFolder);
    }

    // التحميل الكسول (Lazy Loading) لعقد الوكلاء والباقات عند التوسيع
    public async Task LoadChildrenOnExpandAsync(NavigationNodeDto parentNode)
    {
        if (parentNode.Children.Count == 1 && parentNode.Children[0].IsLazyLoadDummy)
        {
            parentNode.Children.Clear();

            if (parentNode.Id == "agents_folder")
            {
                // جلب الوكلاء محلياً من SQLite
                try
                {
                    if (_dbFactory != null)
                    {
                        using var db = await _dbFactory.CreateDbContextAsync();
                        var agents = await db.Agents
                            .Where(a => a.RouterId == _activeRouterContext.CurrentRouterId && !a.IsDeleted)
                            .OrderBy(a => a.Name)
                            .ToListAsync();

                        foreach (var a in agents)
                        {
                            parentNode.Children.Add(new NavigationNodeDto($"agent:{a.Id}", a.Name, "👤", "agents", a.Id.ToString()));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "فشل التحميل الكسول للوكلاء");
                }
            }
            else if (parentNode.Id == "profiles_folder")
            {
                // جلب الباقات محلياً من SQLite
                try
                {
                    if (_dbFactory != null)
                    {
                        using var db = await _dbFactory.CreateDbContextAsync();
                        var profiles = await db.Profiles
                            .Where(p => p.RouterId == _activeRouterContext.CurrentRouterId)
                            .OrderBy(p => p.Name)
                            .ToListAsync();

                        foreach (var p in profiles)
                        {
                            parentNode.Children.Add(new NavigationNodeDto($"profile:{p.Name}", p.Name, "🏷️", "profiles", p.Name));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "فشل التحميل الكسول للباقات");
                }
            }
        }
    }

    private NavigationNodeDto? FindNodeInTree(IEnumerable<NavigationNodeDto> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id) return node;
            var found = FindNodeInTree(node.Children, id);
            if (found != null) return found;
        }
        return null;
    }

    //  محرك جلب البيانات (Smart Query V2) — Keyset Pagination
    // ══════════════════════════════════════════════════════
    // [C-3 FIX] int بدلاً من bool — Thread-Safe باستخدام Interlocked
    private int _isRefreshingQuery; // 0 = false, 1 = true

    private const int DefaultPageSize = 50;

    private DateTime? _lastLoadedCreatedAt;    // Keyset cursor
    private Guid? _lastLoadedId;               // Tie-breaker cursor
    private bool _hasMoreItems = true;
    private bool _isLoadingMore;

    public bool HasMoreItems { get => _hasMoreItems; private set => SetProperty(ref _hasMoreItems, value); }
    public bool IsLoadingMore { get => _isLoadingMore; private set => SetProperty(ref _isLoadingMore, value); }

    public async Task RefreshCurrentQueryAsync()
    {
        // [C-3 FIX] Atomic guard — منع Re-entrancy بشكل Thread-Safe
        if (Interlocked.CompareExchange(ref _isRefreshingQuery, 1, 0) != 0) return;

        // [C-4 FIX] تخلص آمن من CTS القديم قبل إنشاء جديد
        _queryCts?.Cancel();
        _queryCts?.Dispose();
        _queryCts = new CancellationTokenSource();
        var token = _queryCts.Token;

        ShowStatusBanner(ScreenStatus.Loading, "جاري تحميل البيانات المحلية...");

        try
        {
            var parameters = new VoucherQueryParameters
            {
                RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty,
                SelectedNodeId = _stateTracker.SelectedNodeId,
                SelectedNodeCategory = _stateTracker.SelectedNodeCategory,
                SelectedNodeValue = _stateTracker.SelectedNodeValue,
                SearchText = SearchText,
                FilterStatus = FilterStatus,
                FilterSync = FilterSync,
                FilterProfile = FilterProfile,
                PageNumber = 1,
                PageSize = DefaultPageSize,
                IsExactSearch = IsExactSearch
            };

            // 1. جلب الكروت مصفحة ومصفاة باستخدام Keyset Pagination
            var result = await _queryService.GetVouchersKeysetAsync(parameters, null, null, DefaultPageSize, token);

            // 2. تحديث العداد الشامل للنتائج — [H-4 FIX] عداد واحد فقط
            GlobalCount = result.TotalCount;

            // 3. تحديث الكروت في UI — Smart Diff لتجنب تجميد DataGrid
            await _dispatcherService.InvokeAsync(() =>
            {
                for (int i = 0; i < result.Items.Count; i++)
                {
                    if (i < Vouchers.Count)
                    {
                        if (Vouchers[i].Id != result.Items[i].Id ||
                            Vouchers[i].Status != result.Items[i].Status ||
                            Vouchers[i].SyncStatus != result.Items[i].SyncStatus ||
                            Vouchers[i].IsFavorite != result.Items[i].IsFavorite)
                        {
                            Vouchers[i] = result.Items[i];
                        }
                    }
                    else
                    {
                        Vouchers.Add(result.Items[i]);
                    }
                }
                while (Vouchers.Count > result.Items.Count)
                    Vouchers.RemoveAt(Vouchers.Count - 1);
            });

            // 4. تعيين مؤشرات Keyset للصفحة التالية
            if (result.Items.Count > 0)
            {
                var last = result.Items[^1];
                _lastLoadedCreatedAt = last.CreatedAt;
                _lastLoadedId = last.Id;
                HasMoreItems = result.Items.Count == DefaultPageSize;
            }
            else
            {
                _lastLoadedCreatedAt = null;
                _lastLoadedId = null;
                HasMoreItems = false;
            }

            // 5. تحديث فلاتر الباقات والوكلاء
            await UpdateFiltersDataAsync(token);

            // 6. إخفاء البانر
            var rId = _activeRouterContext.CurrentRouter?.Id ?? Guid.Empty;
            if (rId != Guid.Empty && _backgroundImportManager.IsImporting(rId))
                ShowStatusBanner(ScreenStatus.ImportingLegacyVouchers, "جاري استيراد الكروت بالخلفية...");
            else
                HideStatusBanner();
        }
        catch (OperationCanceledException)
        {
            // تجاهل إلغاء العملية — هذا سلوك متوقع
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل جلب البيانات المصفحة");
            ShowStatusBanner(ScreenStatus.Failed, $"فشل تحميل البيانات: {ex.Message}");
        }
        finally
        {
            // [C-3 FIX] إعادة الحارس Atomically
            Interlocked.Exchange(ref _isRefreshingQuery, 0);
            LastSyncTimeText = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }
    }

    public async Task LoadNextPageAsync()
    {
        if (!HasMoreItems || IsLoadingMore) return;
        IsLoadingMore = true;

        var token = _queryCts?.Token ?? CancellationToken.None;

        try
        {
            var parameters = new VoucherQueryParameters
            {
                RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty,
                SelectedNodeId = _stateTracker.SelectedNodeId,
                SelectedNodeCategory = _stateTracker.SelectedNodeCategory,
                SelectedNodeValue = _stateTracker.SelectedNodeValue,
                SearchText = SearchText,
                FilterStatus = FilterStatus,
                FilterSync = FilterSync,
                FilterProfile = FilterProfile,
                PageNumber = 1,
                PageSize = DefaultPageSize
            };

            var result = await _queryService.GetVouchersKeysetAsync(
                parameters,
                _lastLoadedCreatedAt,
                _lastLoadedId,
                DefaultPageSize,
                token);

            if (result.Items.Count == 0)
            {
                HasMoreItems = false;
                return;
            }

            await _dispatcherService.InvokeAsync(() =>
            {
                foreach (var item in result.Items)
                {
                    Vouchers.Add(item);
                }
            });

            var last = result.Items[^1];
            _lastLoadedCreatedAt = last.CreatedAt;
            _lastLoadedId = last.Id;
            HasMoreItems = result.Items.Count == DefaultPageSize;
        }
        catch (OperationCanceledException)
        {
            // تجاهل إلغاء العملية
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل الصفحة التالية للكروت");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    public async Task InstantSyncAsync()
    {
        if (IsSyncingWithRouter) return;
        IsSyncingWithRouter = true;

        var routerId = _activeRouterContext.CurrentRouterId;
        if (routerId == null || routerId == Guid.Empty)
        {
            IsSyncingWithRouter = false;
            return;
        }

        ShowStatusBanner(ScreenStatus.Syncing, "جاري سحب قاعدة البيانات حياً عبر FTP ومزامنتها...");

        try
        {
            // تشغيل المزامنة بالخلفية دون إيقاف البرنامج
            await Task.Run(async () =>
            {
                await _backgroundImportManager.RunSnapshotSyncAsync(routerId.Value, false, CancellationToken.None);
            });

            _notificationService.ShowSuccess("اكتملت مزامنة الـ Snapshot بنجاح في ثوانٍ معدودة!");
            ShowStatusBanner(ScreenStatus.Updated, "✓ البيانات مزامنة بالكامل مع الـ Snapshot");

            // إخفاء التلقائي بعد 3 ثوانٍ
            _ = Task.Delay(TimeSpan.FromSeconds(3))
                    .ContinueWith(_ => _dispatcherService.InvokeAsync(HideStatusBanner),
                                  TaskScheduler.Default);
        }
        catch (SnapshotMismatchException mismatchEx)
        {
            _logger.LogWarning(mismatchEx, "Snapshot mismatch detected during sync");

            // Show custom warning message on the UI thread and get confirmation
            var proceed = await _dispatcherService.InvokeAsync(() =>
            {
                var result = System.Windows.MessageBox.Show(
                    $"تنبيه أمان: يوجد اختلاف كبير بين الكروت المحلية ({mismatchEx.LocalCount} كرت) والكروت على الراوتر ({mismatchEx.SnapshotCount} كرت).\n\n" +
                    $"نسبة الاختلاف: {mismatchEx.PercentageDifference:F1}%\n\n" +
                    $"هل تريد تجاوز صمام الأمان والمتابعة بمزامنة وتحديث البيانات المحلية؟\n" +
                    $"(ملاحظة: هذا قد يسبب تعديل أو حذف كروت محلية غير مطابقة)",
                    "تحذير مزامنة الكروت",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                return result == System.Windows.MessageBoxResult.Yes;
            });

            if (proceed)
            {
                try
                {
                    ShowStatusBanner(ScreenStatus.Syncing, "جاري فرض سحب ومزامنة البيانات...");
                    await Task.Run(async () =>
                    {
                        await _backgroundImportManager.RunSnapshotSyncAsync(routerId.Value, true, CancellationToken.None);
                    });

                    _notificationService.ShowSuccess("اكتملت المزامنة القسرية بنجاح!");
                    ShowStatusBanner(ScreenStatus.Updated, "✓ تم فرض المزامنة بنجاح");

                    _ = Task.Delay(TimeSpan.FromSeconds(3))
                            .ContinueWith(_ => _dispatcherService.InvokeAsync(HideStatusBanner),
                                          TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "فشلت المزامنة القسرية مع الراوتر");
                    _notificationService.ShowError($"فشلت المزامنة القسرية: {ex.Message}");
                    ShowStatusBanner(ScreenStatus.Failed, $"فشلت المزامنة القسرية: {ex.Message}");
                }
            }
            else
            {
                ShowStatusBanner(ScreenStatus.Failed, "🚫 تم إلغاء المزامنة");
                _notificationService.ShowWarning("تم إلغاء عملية المزامنة ولم يتم تعديل أي بيانات.");
                _ = Task.Delay(TimeSpan.FromSeconds(3))
                        .ContinueWith(_ => _dispatcherService.InvokeAsync(HideStatusBanner),
                                      TaskScheduler.Default);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشلت المزامنة الكاملة مع الراوتر");
            _notificationService.ShowError($"فشلت المزامنة مع الراوتر: {ex.Message}");
            ShowStatusBanner(ScreenStatus.Failed, $"فشلت المزامنة: {ex.Message}");
        }
        finally
        {
            IsSyncingWithRouter = false;
            // تحديث البيانات المحلية
            await RefreshCurrentQueryAsync();
            await UpdateRouterStatusWidgetAsync();
        }
    }

    private async Task UpdateFiltersDataAsync(CancellationToken token)
    {
        try
        {
            if (_dbFactory == null) return;

            using var db = await _dbFactory.CreateDbContextAsync(token);
            var routerId = _activeRouterContext.CurrentRouterId;

            var profiles = await db.Vouchers
                .Where(v => v.RouterId == routerId && !v.IsDeleted)
                .Select(v => v.ProfileName)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync(token);

            var agents = await db.Agents
                .Where(a => a.RouterId == routerId)
                .Select(a => new { a.Id, a.Name })
                .OrderBy(a => a.Name)
                .ToListAsync(token);

            await _dispatcherService.InvokeAsync(() =>
            {
                ProfileFilters.Clear();
                ProfileFilters.Add("كل الباقات");
                foreach (var p in profiles)
                {
                    ProfileFilters.Add(p);
                }

                AgentFilters.Clear();
                AgentFilters.Add(new AgentFilterItem(Guid.Empty, "كل الوكلاء"));
                foreach (var a in agents)
                {
                    AgentFilters.Add(new AgentFilterItem(a.Id, a.Name));
                }
            });
        }
        catch (OperationCanceledException)
        {
            // تجاهل إلغاء العملية
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ [UpdateFilters] فشل تحديث قائمة الباقات أو الوكلاء الجانبية");
        }
    }



    // ══════════════════════════════════════════════════════
    //  تحديث حالة الـ Router Widget والثابت
    // ══════════════════════════════════════════════════════
    private async Task UpdateRouterStatusWidgetAsync()
    {
        try
        {
            if (_dbFactory != null)
            {
                using var db = await _dbFactory.CreateDbContextAsync();
                
                // معلق ومفشول
                PendingSyncCount = await db.Vouchers
                    .Where(v => v.RouterId == _activeRouterContext.CurrentRouterId && v.SyncStatus == SyncStatus.Pending)
                    .CountAsync();

                FailedSyncCount = await db.Vouchers
                    .Where(v => v.RouterId == _activeRouterContext.CurrentRouterId && v.SyncStatus == SyncStatus.Failed)
                    .CountAsync();
            }
        }
        catch
        {
            // تجاهل الأعطال للـ widget
        }
    }

    // ══════════════════════════════════════════════════════
    //  البحث المتأخر (Search Debouncer)
    // ══════════════════════════════════════════════════════
    private void TriggerDebouncedSearch()
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();
        FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
    }

    // ══════════════════════════════════════════════════════
    //  البانر المنزلق (Banner Messages)
    // ══════════════════════════════════════════════════════
    private void ShowStatusBanner(ScreenStatus status, string message)
    {
        CurrentScreenStatus = status;
        StatusBannerMessage = message;
        IsStatusBannerVisible = true;
    }

    private void HideStatusBanner()
    {
        IsStatusBannerVisible = false;
    }

    // ══════════════════════════════════════════════════════
    //  المراقب الدوري المضمون (Sync Watchdog)
    //  هذا هو الحل الجذري النهائي: لا يعتمد على أي سلسلة أحداث
    // ══════════════════════════════════════════════════════
    private void StartSyncWatchdog()
    {
        if (_watchdogStarted) return;
        _watchdogStarted = true;

        _syncWatchdogTimer = new System.Threading.Timer(async _ =>
        {
            if (System.Threading.Interlocked.CompareExchange(ref _isSyncRunningInt, 1, 0) != 0) return;
            try
            {
                if (!_activeRouterContext.IsConnected) return;
                var routerId = _activeRouterContext.CurrentRouterId;
                if (!routerId.HasValue || routerId.Value == Guid.Empty) return;

                int pendingCount;
                int failedCount;
                try
                {
                    await using var db = await _dbFactory.CreateDbContextAsync();
                    pendingCount = await db.Vouchers
                        .IgnoreQueryFilters()
                        .CountAsync(v => v.RouterId == routerId.Value && !v.IsDeleted
                                      && v.SyncStatus == MikroTikVoucherPrinter.Domain.Enums.SyncStatus.Pending);
                    failedCount = await db.Vouchers
                        .IgnoreQueryFilters()
                        .CountAsync(v => v.RouterId == routerId.Value && !v.IsDeleted
                                      && v.SyncStatus == MikroTikVoucherPrinter.Domain.Enums.SyncStatus.Failed);
                }
                catch { return; }

                if (pendingCount == 0 && failedCount == 0) return;

                _logger.LogInformation("[WATCHDOG] {P} pending + {F} failed -> starting sync", pendingCount, failedCount);

                _dispatcherService.InvokeAsync(() =>
                {
                    PendingSyncCount = pendingCount;
                    FailedSyncCount = failedCount;
                    ShowStatusBanner(ScreenStatus.PendingChanges,
                        $"جاري مزامنة {pendingCount + failedCount} كرت مع الراوتر تلقائياً...");
                });

                SyncMetrics metrics;
                if (failedCount > 0)
                    metrics = await _syncService.RetryFailedAsync(CancellationToken.None);
                else
                    metrics = await _syncService.ProcessPendingAsync(CancellationToken.None);

                _logger.LogInformation("[WATCHDOG] Done: Success={S} Failed={F}", metrics.Success, metrics.Failed);

                await _dispatcherService.InvokeAsync(async () =>
                {
                    await UpdateRouterStatusWidgetAsync();
                    await RefreshCurrentQueryAsync();
                    if (metrics.Success > 0)
                    {
                        LastSyncTimeText = DateTime.Now.ToString("HH:mm:ss");
                        var syncedIds = metrics.SyncedVoucherIds;
                        ShowStatusBanner(ScreenStatus.Updated, $"تمت مزامنة {metrics.Success} كرت بنجاح!");
                        var choice = System.Windows.MessageBox.Show(
                            $"تمت مزامنة {metrics.Success} كرت مع الراوتر.\nهل تريد طباعة PDF الآن؟",
                            "استئناف الطباعة",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Information);
                        if (choice == System.Windows.MessageBoxResult.Yes)
                            await PrintSelectedAsync(syncedIds);
                    }
                    else if (metrics.Failed > 0)
                        ShowStatusBanner(ScreenStatus.Failed, $"فشلت مزامنة {metrics.Failed} كرت. سيعاد المحاولة.");
                    else
                        HideStatusBanner();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WATCHDOG] Exception during sync");
                _dispatcherService.InvokeAsync(() =>
                    ShowStatusBanner(ScreenStatus.Failed, $"خطأ في المزامنة التلقائية: {ex.Message}"));
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isSyncRunningInt, 0);
            }
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8));
    }
    private int _isSyncRunningInt = 0;

    // ══════════════════════════════════════════════════════
    //  مزامنة الخلفية الذكية (Background Sync)
    // ══════════════════════════════════════════════════════
    private void TriggerBackgroundSync()
    {
        int totalUnsynced = PendingSyncCount + FailedSyncCount;
        if (totalUnsynced == 0) return;

        ShowStatusBanner(ScreenStatus.PendingChanges, $"توجد {totalUnsynced} كروت بانتظار المزامنة مع الراوتر...");

        _ = Task.Run(async () =>
        {
            try
            {
                // إعداد الـ Progress
                var progress = new Progress<(int success, int failed, int total)>(p =>
                {
                    _dispatcherService.InvokeAsync(() =>
                    {
                        SyncProcessedCount = p.success + p.failed;
                        SyncTotalCount = p.total;
                        SyncProgressPercent = (double)SyncProcessedCount / SyncTotalCount * 100;
                        
                        ShowStatusBanner(ScreenStatus.Syncing, 
                            $"تتم مزامنة {SyncProcessedCount} من أصل {SyncTotalCount} كرت... [{(int)SyncProgressPercent}%]");
                    });
                });

                var token = CancellationToken.None;
                SyncMetrics metrics;

                if (FailedSyncCount > 0)
                {
                    // RetryFailedAsync: تحول Failed→Pending ثم تستدعي ProcessPendingAsync داخلياً وترجع النتيجة المدمجة
                    metrics = await _syncService.RetryFailedAsync(token);
                    // إذا كان هناك كروت Pending إضافية (غير Failed) نعالجها أيضاً
                    if (PendingSyncCount > 0)
                    {
                        var extraMetrics = await _syncService.ProcessPendingAsync(progress, token);
                        metrics = metrics.Merge(extraMetrics);
                    }
                }
                else
                {
                    // فقط Pending
                    metrics = await _syncService.ProcessPendingAsync(progress, token);
                }

                await _dispatcherService.InvokeAsync(async () =>
                {
                    await UpdateRouterStatusWidgetAsync();
                    await RefreshCurrentQueryAsync();
                    
                    if (metrics.Failed > 0)
                    {
                        ShowStatusBanner(ScreenStatus.Failed, $"اكتملت المزامنة مع وجود أخطاء: نجح {metrics.Success} | فشل {metrics.Failed}");
                    }
                    else if (metrics.Success > 0)
                    {
                        ShowStatusBanner(ScreenStatus.Updated, $"✓ اكتملت مزامنة {metrics.Success} كرت بنجاح مع الراوتر! يمكنك الآن حفظ وتصدير ملف PDF للطباعة.");
                        LastSyncTimeText = DateTime.Now.ToString("HH:mm:ss");

                        var userChoice = System.Windows.MessageBox.Show(
                            $"🎉 تمت مزامنة {metrics.Success} كرت مع الراوتر بنجاح!\n\nهل ترغب في حفظ وطباعة ملف PDF لهذه الكروت الآن؟",
                            "استئناف الطباعة والتصدير",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Information);

                        if (userChoice == System.Windows.MessageBoxResult.Yes)
                        {
                            await PrintSelectedAsync(metrics.SyncedVoucherIds);
                        }
                    }
                    else
                    {
                        ShowStatusBanner(ScreenStatus.Updated, "✓ البيانات مزامنة بالكامل مع الراوتر");
                        LastSyncTimeText = DateTime.Now.ToString("HH:mm:ss");
                        
                        // إخفاء تلقائي بعد 3 ثوانٍ
                        _ = Task.Delay(TimeSpan.FromSeconds(3))
                                .ContinueWith(_ => _dispatcherService.InvokeAsync(HideStatusBanner),
                                              TaskScheduler.Default);
                    }

                    // تحديث ذكي للجدول الحالي
                    await RefreshCurrentQueryAsync();
                });
            }
            catch (Exception ex)
            {
                _dispatcherService.InvokeAsync(() =>
                {
                    ShowStatusBanner(ScreenStatus.Failed, $"فشلت المزامنة: {ex.Message}");
                });
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isSyncRunningInt, 0);
            }
        });
    }

    // ══════════════════════════════════════════════════════
    //  تعديل وحذف الكروت
    // ══════════════════════════════════════════════════════
    private async Task DeleteSelectedAsync()
    {
        if (SelectedCount == 0) return;

        var selectedIds = SelectedVoucherIds.ToList();
        int totalSelected = selectedIds.Count;

        try
        {
            await ExecuteBusyAsync(async (token) =>
            {
                int deleted = 0;
                int failed  = 0;

                try
                {
                    (deleted, failed) = await _managementService.SoftDeleteVouchersAsync(selectedIds, token);
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "❌ [Delete] استثناء داخل SoftDeleteVouchersAsync");
                    await _dispatcherService.InvokeAsync(() =>
                    {
                        _notificationService.ShowError(
                            $"❌ خطأ غير متوقع أثناء الحذف:\n{innerEx.Message}",
                            "خطأ في الحذف");
                    });
                    throw;
                }

                await _dispatcherService.InvokeAsync(() =>
                {
                    SelectedVoucherIds.Clear();
                    SelectedCount = 0;

                    if (deleted == 0 && failed == 0)
                    {
                        _notificationService.ShowError(
                            "⚠️ لم يُحذف أي كرت — لم يتم إيجاد الكروت على الراوتر أو في قاعدة البيانات.",
                            "تنبيه");
                    }
                    else if (failed > 0 && deleted == 0)
                    {
                        _notificationService.ShowError(
                            $"❌ فشل حذف جميع الكروت ({failed}/{totalSelected})\nتأكد من اتصال المايكروتك.",
                            "فشل الحذف");
                    }
                    else if (failed > 0)
                    {
                        _notificationService.ShowError(
                            $"⚠️ تم حذف {deleted} كرت بنجاح، لكن فشل {failed} كرت من الراوتر.",
                            "حذف جزئي");
                    }
                    else
                    {
                        _notificationService.ShowSuccess(
                            $"✅ تم حذف {deleted} كرت بنجاح من الراوتر وقاعدة البيانات.",
                            "تم الحذف");
                    }
                });

                GlobalCount -= deleted;

                _logger.LogInformation("🗑️ [Delete] نجح {Deleted} | فشل {Failed} من أصل {Total}", deleted, failed, totalSelected);

                await RefreshCurrentQueryAsync();
                await UpdateRouterStatusWidgetAsync();

            }, $"جارٍ حذف {totalSelected} كرت...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [Delete] استثناء خارج ExecuteBusyAsync");
            await _dispatcherService.InvokeAsync(() =>
            {
                _notificationService.ShowError(
                    $"❌ خطأ حرج أثناء الحذف:\n{ex.GetType().Name}: {ex.Message}",
                    "خطأ حرج");
            });
            throw;
        }
    }

    private async Task RestoreSelectedAsync()
    {
        if (SelectedCount == 0) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var selectedIds = SelectedVoucherIds.ToList();
            var results = await _managementService.RestoreVouchersAsync(selectedIds, token);
            int restored = results.Count(r => r.Status == MikroTikVoucherPrinter.Application.DTOs.RestoreStatus.Success || r.Status == MikroTikVoucherPrinter.Application.DTOs.RestoreStatus.AlreadyExistsReconciled);
            int failed = results.Count(r => r.Status != MikroTikVoucherPrinter.Application.DTOs.RestoreStatus.Success && r.Status != MikroTikVoucherPrinter.Application.DTOs.RestoreStatus.AlreadyExistsReconciled);
            var firstError = results.FirstOrDefault(r => !string.IsNullOrEmpty(r.ErrorMessage))?.ErrorMessage;

            await _dispatcherService.InvokeAsync(() =>
            {
                SelectedVoucherIds.Clear();
                SelectedCount = 0;
            });

            // Update local counts
            GlobalCount += restored;

            _logger.LogInformation("Restore Vouchers: Success {Restored} | Failed {Failed}", restored, failed);

            if (failed > 0 && !string.IsNullOrEmpty(firstError))
            {
                _notificationService.ShowWarning($"Failed to restore some vouchers: {firstError}");
            }
            else if (restored > 0)
            {
                _notificationService.ShowSuccess($"Successfully restored {restored} vouchers.");
            }

            await RefreshCurrentQueryAsync();
            await UpdateRouterStatusWidgetAsync();
        });
    }

    private async Task PrintSelectedAsync(IEnumerable<Guid>? explicitVoucherIds = null)
    {
        List<Guid> idsToFetch;
        var explicitList = explicitVoucherIds?.ToList();

        if (explicitList != null && explicitList.Count > 0)
        {
            idsToFetch = explicitList;
        }
        else
        {
            var selectedIds = SelectedVoucherIds.ToList();
            if (selectedIds.Count > 0)
            {
                idsToFetch = selectedIds;
            }
            else
            {
                idsToFetch = Vouchers.Select(v => v.Id).ToList();
            }
        }

        if (idsToFetch.Count == 0)
        {
            _notificationService.ShowWarning("لا توجد كروت محددة أو معروضة لطباعتها.");
            return;
        }

        if (!_featureAuthorizationService.CanExecute(FeatureId.VoucherPrinting, idsToFetch.Count))
        {
            _notificationService.ShowError($"لا يمكن طباعة أكثر من {SecurityConfiguration.MaxFreeVouchersLimit} كرت في النسخة المجانية. يرجى تفعيل البرنامج.");
            return;
        }

        await ExecuteBusyAsync(async (token) =>
        {
            IReadOnlyList<VoucherDto> selected;

            using var db = await _dbFactory.CreateDbContextAsync(token);
            selected = await db.Vouchers
                .IgnoreQueryFilters()
                .Include(v => v.Agent)
                .AsNoTracking()
                .Where(v => idsToFetch.Contains(v.Id))
                .Select(v => new VoucherDto
                {
                    Id         = v.Id,
                    Username   = v.Username,
                    Password   = v.Password,
                    Profile    = v.ProfileName,
                    Price      = v.Price,
                    Status     = v.Status,
                    AgentName  = v.Agent != null ? v.Agent.Name : "-"
                })
                .ToListAsync(token);

            if (selected.Count == 0) return;

            Guid? effectiveTemplateId = null;

            // 1. فحص ما إذا كانت باقة الكروت تحتوي على قالب طباعة مخصص
            var firstProfileName = selected.FirstOrDefault(v => !string.IsNullOrEmpty(v.Profile))?.Profile;
            if (!string.IsNullOrEmpty(firstProfileName))
            {
                var routerId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
                var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Name == firstProfileName && p.RouterId == routerId, token);
                if (profile != null && profile.TemplateId.HasValue && profile.TemplateId.Value != Guid.Empty)
                {
                    effectiveTemplateId = profile.TemplateId.Value;
                }
            }

            // 2. البديل الثاني: القالب المحدد أخيراً في إعدادات التوليد
            if (!effectiveTemplateId.HasValue)
            {
                var lastSavedTemplateIdStr = _settingsService.Get("Print.LastGenerateTemplateId", string.Empty);
                if (Guid.TryParse(lastSavedTemplateIdStr, out var lastGuid) && lastGuid != Guid.Empty)
                {
                    effectiveTemplateId = lastGuid;
                }
            }

            // 3. البديل الثالث: القالب الأساسي للنظام
            if (!effectiveTemplateId.HasValue)
            {
                var primarySysTid = await _templateService.GetPrimarySystemTemplateIdAsync();
                if (primarySysTid != Guid.Empty)
                {
                    effectiveTemplateId = primarySysTid;
                }
            }

            var settings = new PrintSettingsDto
            {
                CompressOutput = true,
                ImageQuality = 45,
                MaxImageSidePx = 400
            };

            if (effectiveTemplateId.HasValue)
            {
                settings.CustomTemplateId = effectiveTemplateId.Value;
            }

            var result = await _printService.GeneratePdfAsync(new List<VoucherDto>(selected), settings, cancellationToken: token);

            if (result.IsSuccess)
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "حفظ ملف PDF الكروت للطباعة",
                    Filter = "ملفات PDF (*.pdf)|*.pdf",
                    DefaultExt = "pdf",
                    FileName = $"LuxCard_Vouchers_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                };

                string targetPath;
                if (saveFileDialog.ShowDialog() == true)
                {
                    targetPath = saveFileDialog.FileName;
                }
                else
                {
                    targetPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"luxcard_selected_{DateTime.Now:HHmmss}.pdf");
                }

                await System.IO.File.WriteAllBytesAsync(targetPath, result.Value, token);
                await _printPreviewService.PreviewPdfAsync(result.Value, targetPath, token);
                _notificationService.ShowSuccess($"تم حفظ وتوليد PDF لـ {selected.Count} كرت بنجاح في:\n{targetPath}");

                // تحديث حالة الطباعة في قاعدة البيانات وفي الكروت المعروضة
                try
                {
                    var selectedIds = selected.Select(v => v.Id).ToList();
                    await using var printDbContext = await _dbFactory.CreateDbContextAsync(token);
                    var dbVouchers = await printDbContext.Vouchers.Where(v => selectedIds.Contains(v.Id)).ToListAsync(token);
                    foreach (var dbV in dbVouchers)
                    {
                        dbV.PrintStatus = MikroTikVoucherPrinter.Domain.Enums.VoucherPrintStatus.Printed;
                    }
                    await printDbContext.SaveChangesAsync(token);

                    foreach (var item in selected)
                    {
                        item.PrintStatus = MikroTikVoucherPrinter.Domain.Enums.VoucherPrintStatus.Printed;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "فشل تحديث حالة الطباعة للكروت في قاعدة البيانات");
                }
            }
            else
            {
                _notificationService.ShowError($"فشلت الطباعة: {result.ErrorMessage}");
            }
        });
    }

    private async Task RetryFailedDataAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var metrics = await _syncService.RetryFailedAsync(token);
            await UpdateRouterStatusWidgetAsync();
            TriggerBackgroundSync();
        });
    }

    private void ClearFilters()
    {
        _stateTracker.Reset();
        SearchText = "";
        FilterStatus = "All";
        FilterSync = "All";
        FilterProfile = "كل الباقات";
        FilterAgentId = Guid.Empty;
        SelectedQuickFilter = "all";
        FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
    }

    private async Task ShowSessionsForVoucherAsync(VoucherDto? v)
    {
        if (v == null) return;
        await ExecuteBusyAsync(async (token) =>
        {
            var lines = await _queryService.GetHotspotActiveSessionsForUserAsync(v.Username, token);
            var text = string.Join(Environment.NewLine, lines);
            await _dispatcherService.InvokeAsync(() =>
            {
                _notificationService.ShowInformation(text, $"جلسات: {v.Username}");
            });
        }, "جاري جلب الجلسات النشطة...");
    }

    private async Task ShowVoucherDetailsAsync(VoucherDto? v)
    {
        if (v == null || _activeRouterContext.CurrentRouter == null) return;
        var routerId = _activeRouterContext.CurrentRouter.Id;
        
        await ExecuteBusyAsync(async (token) =>
        {
            var dbPath = _backgroundImportManager.GetCachedCleanDbPath(routerId);
            if (string.IsNullOrEmpty(dbPath) || !System.IO.File.Exists(dbPath))
            {
                await _backgroundImportManager.DownloadAndCacheDbAsync(routerId, token);
                dbPath = _backgroundImportManager.GetCachedCleanDbPath(routerId);
            }

            if (string.IsNullOrEmpty(dbPath) || !System.IO.File.Exists(dbPath))
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
                var window = new Views.UserReportWindow(v.Username, dbPath, routerName, leases, v);
                window.Owner = System.Windows.Application.Current.MainWindow;
                window.ShowDialog();
            });
        }, "جاري جلب تفاصيل الجلسات والبيانات...");
    }

    private void CopyUsername(VoucherDto? v)
    {
        if (v == null || string.IsNullOrEmpty(v.Username)) return;
        _clipboardService.SetText(v.Username);
        _notificationService.ShowSuccess("تم نسخ اسم المستخدم!");
    }

    private void CopyPassword(VoucherDto? v)
    {
        if (v == null || string.IsNullOrEmpty(v.Password)) return;
        _clipboardService.SetText(v.Password);
        _notificationService.ShowSuccess("تم نسخ كلمة السر!");
    }

    // ══════════════════════════════════════════════════════
    //  عمليات استيراد كروت الراوتر (Wizard Operations)
    // ══════════════════════════════════════════════════════
    private async Task StartBackgroundImportAsync()
    {
        if (_activeRouterContext.CurrentRouter == null) return;
        var routerId = _activeRouterContext.CurrentRouter.Id;

        IsFirstRunImportRequired = false;
        IsImporting = true;
        IsImportDetailsHidden = false;
        IsImportPaused = false;
        ImportProgressPercent = 0;
        ImportedCount = 0;
        TotalImportCount = 0;

        ShowStatusBanner(ScreenStatus.ImportingLegacyVouchers, "جاري بدء استيراد الكروت بالخلفية...");

        // بدء الاستيراد في الخلفية
        _backgroundImportManager.StartImport(routerId);

        // فتح الشاشة وبناء الشجرة فوراً
        BuildNavigationTree();
        await RefreshCurrentQueryAsync();
        TriggerBackgroundSync();
    }

    private void PauseImport()
    {
        if (_activeRouterContext.CurrentRouter == null) return;
        _backgroundImportManager.PauseImport(_activeRouterContext.CurrentRouter.Id);
    }

    private void ResumeImport()
    {
        if (_activeRouterContext.CurrentRouter == null) return;
        _backgroundImportManager.ResumeImport(_activeRouterContext.CurrentRouter.Id);
    }

    private void HideImportDetails()
    {
        IsImportDetailsHidden = true;
    }

    // معالجات أحداث الاستيراد في الخلفية
    private void OnProgressChanged(object? sender, VoucherImportProgressEventArgs e)
    {
        if (_activeRouterContext.CurrentRouter?.Id != e.RouterId) return;

        _dispatcherService.InvokeAsync(() =>
        {
            IsImporting = true;
            IsFirstRunImportRequired = false;
            ImportedCount = e.ImportedCount;
            TotalImportCount = e.TotalCount;
            ImportProgressPercent = e.ProgressPercent;
            IsImportPaused = e.IsPaused;

            ShowStatusBanner(ScreenStatus.ImportingLegacyVouchers, $"جاري استيراد الكروت بالخلفية... تم استيراد {ImportedCount} من {TotalImportCount}");

            // تحديث العداد الشامل
            GlobalCount = e.ImportedCount;
        });
    }

    private void OnImportCompleted(object? sender, Guid routerId)
    {
        if (_activeRouterContext.CurrentRouter?.Id != routerId) return;

        _dispatcherService.InvokeAsync(async () =>
        {
            IsImporting = false;
            IsFirstRunImportRequired = false;
            _notificationService.ShowSuccess("تم استيراد كروت الراوتر بالخلفية بنجاح!");
            HideStatusBanner();

            // إعادة بناء الشجرة والتحديث الأخير
            BuildNavigationTree();
            await RefreshCurrentQueryAsync();
        });
    }

    private void OnImportError(object? sender, VoucherImportErrorEventArgs e)
    {
        if (_activeRouterContext.CurrentRouter?.Id != e.RouterId) return;

        _dispatcherService.InvokeAsync(() =>
        {
            IsImporting = false;
            _notificationService.ShowError($"فشل استيراد الكروت بالخلفية: {e.ErrorMessage}");
            ShowStatusBanner(ScreenStatus.Failed, $"فشل الاستيراد: {e.ErrorMessage}");
        });
    }

    private async void OnAutoRefreshTriggered(AutoRefreshTriggeredEvent message)
    {
        // [H-5 FIX] async void تحتاج try-catch لمنع سقوط التطبيق
        try
        {
            _logger.LogInformation("🔄 [AutoRefresh] Received AutoRefreshTriggeredEvent.");
            if (IsBusy || Interlocked.CompareExchange(ref _isRefreshingQuery, 0, 0) == 1)
            {
                _logger.LogInformation("🔄 [AutoRefresh] Skipped — ViewModel busy.");
                return;
            }
            await _dispatcherService.InvokeAsync(async () =>
            {
                await RefreshCurrentQueryAsync();
                await UpdateRouterStatusWidgetAsync();
                _logger.LogInformation("🔄 [AutoRefresh] UI refreshed.");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [AutoRefresh] خطأ غير معالج في دورة التحديث التلقائي");
        }
    }

    // [H-1 FIX] حذفنا StartImportAsync التي كانت wrapper ميت لـ StartBackgroundImportAsync

    private void DismissImport()
    {
        IsFirstRunImportRequired = false;
        
        // استئناف التهيئة المعتادة
        if (_stateTracker.HasSavedState)
        {
            var savedNode = FindNodeInTree(NavigationTree, _stateTracker.SelectedNodeId);
            if (savedNode != null)
            {
                _selectedNode = savedNode;
                OnPropertyChanged(nameof(SelectedNode));
            }
        }
        
        FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
        TriggerBackgroundSync();
    }

    public override void Dispose()
    {
        // [C-4 FIX] تخلص صحيح من CancellationTokenSource
        _queryCts?.Cancel();
        _queryCts?.Dispose();
        _debounceTimer?.Stop();

        // إلغاء الاشتراك من الأحداث لتجنب تسريب الذاكرة
        _backgroundImportManager.ProgressChanged -= OnProgressChanged;
        _backgroundImportManager.ImportCompleted -= OnImportCompleted;
        _backgroundImportManager.ImportError -= OnImportError;
        _eventBus.Unsubscribe<AutoRefreshTriggeredEvent>(this);

        base.Dispose();
    }

    // [C-2 FIX] Stub implementations — تصلح الـ Broken Bindings في XAML
    private async Task CreateBatchAsync()
    {
        await _dispatcherService.InvokeAsync(() =>
        {
            var dialog = new Views.CreateBatchDialog(
                _dbFactory,
                _syncService,
                _printService,
                _templateService,
                _settingsService,
                _activeRouterContext,
                _shellState,
                _logger,
                _featureAuthorizationService,
                _profileService);
            var mainWin = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w is Lux.Management.Console.MainWindow);
            if (mainWin != null && mainWin != dialog)
            {
                dialog.Owner = mainWin;
            }
            var result = dialog.ShowDialog();
            if (result == true)
            {
                FireAndForget(RefreshCurrentQueryAsync, "RefreshCurrentQuery");
            }
        });
    }

    private Task ExportSelectedAsync()
    {
        // TODO: تنفيذ وظيفة تصدير الكروت المحددة
        _logger.LogInformation("ℹ️ ExportSelected — لم يُنفّذ بعد");
        return Task.CompletedTask;
    }

    private async Task ToggleFavoriteAsync(VoucherDto? voucherParam)
    {
        var target = voucherParam ?? SelectedVoucher;
        if (target == null)
        {
            _notificationService.ShowWarning("يرجى تحديد كرت أولاً لإضافته أو إزالته من المفضلة.");
            return;
        }

        SelectedVoucher = target;

        try
        {
            bool newFavoriteState = !target.IsFavorite;

            await using var db = await _dbFactory.CreateDbContextAsync();
            var activeRouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
            var entity = await db.Vouchers.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == target.Id);
            
            if (entity == null && !string.IsNullOrEmpty(target.Username))
            {
                entity = await db.Vouchers.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Username == target.Username && v.RouterId == activeRouterId);
            }

            if (entity != null)
            {
                entity.IsFavorite = newFavoriteState;
                await db.SaveChangesAsync();
            }
            else if (activeRouterId != Guid.Empty && !string.IsNullOrEmpty(target.Username))
            {
                entity = new MikroTikVoucherPrinter.Domain.Entities.Voucher
                {
                    Id = target.Id != Guid.Empty ? target.Id : Guid.NewGuid(),
                    Username = target.Username,
                    Password = target.Password ?? "",
                    ProfileName = target.Profile ?? "",
                    Price = target.Price,
                    Status = target.Status,
                    IsFavorite = newFavoriteState,
                    RouterId = activeRouterId,
                    VoucherSource = MikroTikVoucherPrinter.Domain.Enums.VoucherSource.ImportedFromRouter,
                    CreatedAt = target.CreatedAt != default ? target.CreatedAt : DateTime.UtcNow
                };
                db.Vouchers.Add(entity);
                await db.SaveChangesAsync();
            }

            target.IsFavorite = newFavoriteState;

            if (newFavoriteState)
            {
                _notificationService.ShowSuccess($"تمت إضافة الكرت ({target.Username}) إلى المفضلة ⭐");
            }
            else
            {
                _notificationService.ShowInformation($"تمت إزالة الكرت ({target.Username}) من المفضلة ⭐");
            }

            if (_stateTracker.FilterStatus is "Favorite" or "المفضلة")
            {
                await RefreshCurrentQueryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تغيير حالة المفضلة للكرت {Username}", target.Username);
            _notificationService.ShowError($"حدث خطأ أثناء تغيير حالة المفضلة: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RechargeVoucherAsync(VoucherDto? voucher)
    {
        var target = voucher ?? SelectedVoucher;
        if (target == null)
        {
            _notificationService.ShowWarning("يرجى تحديد كرت أولاً لعملية إعادة الشحن.", "تنبيه");
            return;
        }

        // 1. جلب قائمة الباقات المتاحة
        List<string> availableProfiles = new();
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            availableProfiles = await db.Profiles
                .Select(p => p.Name)
                .Distinct()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذر جلب الباقات من قاعدة البيانات المحلية");
        }

        if (availableProfiles.Count == 0 && ProfileFilters.Count > 1)
        {
            availableProfiles = ProfileFilters.Where(p => p != "كل الباقات").ToList();
        }

        // 2. عرض مربع حوار اختيار الباقة
        var dialog = new SelectProfileDialog(
            "⚡ إعادة شحن الكرت",
            target.Username,
            availableProfiles,
            target.Profile)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true || string.IsNullOrEmpty(dialog.SelectedProfileName))
        {
            return;
        }

        string selectedProfile = dialog.SelectedProfileName;

        // 3. التنفيذ على الراوتر وقاعدة البيانات
        await ExecuteBusyAsync(async (token) =>
        {
            try
            {
                // محاولة تفعيل الباقة في المايكروتك يوزر مانجر v6
                var res = await _routerService.ExecuteCommandAsync(
                    "/tool/user-manager/user/create-and-activate-profile",
                    new Dictionary<string, string>
                    {
                        { "customer", "admin" },
                        { "user", target.Username },
                        { "profile", selectedProfile }
                    }, token);

                if (!res.Success)
                {
                    // محاولة تفعيل الباقة في المايكروتك يوزر مانجر v7
                    await _routerService.ExecuteCommandAsync(
                        "/user-manager/user/profile/add",
                        new Dictionary<string, string>
                        {
                            { "user", target.Username },
                            { "profile", selectedProfile }
                        }, token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تنبيه أثناء تنفيذ أمر إعادة الشحن على الراوتر للكرت {Username}", target.Username);
            }

            // 4. تحديث سجل الكرت محلياً في قاعدة البيانات
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(token);
                var entity = await db.Vouchers.FirstOrDefaultAsync(v => v.Username == target.Username, token);
                if (entity != null)
                {
                    entity.ProfileName = selectedProfile;
                    entity.BytesUsed = 0;
                    entity.DownloadUsedBytes = 0;
                    entity.UploadUsedBytes = 0;
                    entity.UptimeUsedSeconds = 0;
                    entity.IsDisabled = false;
                    entity.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ أثناء تحديث سجل الكرت محلياً {Username}", target.Username);
            }

            // 5. تحديث الكائن المعروض في الشاشة
            target.Profile = selectedProfile;
            target.QuotaUsedBytes = 0;
            target.IsDisabled = false;

            await _dispatcherService.InvokeAsync(() =>
            {
                _notificationService.ShowSuccess(
                    $"✅ تم إعادة شحن الكرت ({target.Username}) بنجاح بباقة ({selectedProfile}).",
                    "إعادة شحن الكرت");
            });

            await RefreshCurrentQueryAsync();
        }, $"جارٍ إعادة شحن الكرت ({target.Username})...");
    }

    [RelayCommand]
    private async Task RecreateVoucherAsync(VoucherDto? voucher)
    {
        var target = voucher ?? SelectedVoucher;
        if (target == null)
        {
            _notificationService.ShowWarning("يرجى تحديد كرت أولاً لعملية إعادة الإنشاء.", "تنبيه");
            return;
        }

        // 1. جلب قائمة الباقات المتاحة
        List<string> availableProfiles = new();
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            availableProfiles = await db.Profiles
                .Select(p => p.Name)
                .Distinct()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذر جلب الباقات من قاعدة البيانات المحلية");
        }

        if (availableProfiles.Count == 0 && ProfileFilters.Count > 1)
        {
            availableProfiles = ProfileFilters.Where(p => p != "كل الباقات").ToList();
        }

        // 2. عرض مربع حوار اختيار الباقة
        var dialog = new SelectProfileDialog(
            "🔄 إعادة إنشاء الكرت",
            target.Username,
            availableProfiles,
            target.Profile)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true || string.IsNullOrEmpty(dialog.SelectedProfileName))
        {
            return;
        }

        string selectedProfile = dialog.SelectedProfileName;

        // 3. التنفيذ (حذف ثم إعادة إنشاء الكرت)
        await ExecuteBusyAsync(async (token) =>
        {
            try
            {
                // أولاً: حذف المستخدم من المايكروتك يوزر مانجر
                await _routerService.ExecuteCommandAsync(
                    "/tool/user-manager/user/remove",
                    new Dictionary<string, string> { { "numbers", target.Username } }, token);

                // ثانياً: إعادة إنشاء المستخدم في المايكروتك يوزر مانجر
                await _routerService.ExecuteCommandAsync(
                    "/tool/user-manager/user/add",
                    new Dictionary<string, string>
                    {
                        { "customer", "admin" },
                        { "username", target.Username },
                        { "password", target.Password ?? target.Username }
                    }, token);

                // ثالثاً: إضافة وتفعيل الباقة للمستخدم
                await _routerService.ExecuteCommandAsync(
                    "/tool/user-manager/user/create-and-activate-profile",
                    new Dictionary<string, string>
                    {
                        { "customer", "admin" },
                        { "user", target.Username },
                        { "profile", selectedProfile }
                    }, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تنبيه أثناء تنفيذ أمر إعادة إنشاء الكرت على الراوتر {Username}", target.Username);
            }

            // 4. تحديث سجل الكرت محلياً في قاعدة البيانات
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(token);
                var entity = await db.Vouchers.FirstOrDefaultAsync(v => v.Username == target.Username, token);
                if (entity != null)
                {
                    entity.ProfileName = selectedProfile;
                    entity.BytesUsed = 0;
                    entity.DownloadUsedBytes = 0;
                    entity.UploadUsedBytes = 0;
                    entity.UptimeUsedSeconds = 0;
                    entity.IsDisabled = false;
                    entity.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ أثناء تحديث سجل الكرت محلياً {Username}", target.Username);
            }

            // 5. تحديث الكائن المعروض في الشاشة
            target.Profile = selectedProfile;
            target.QuotaUsedBytes = 0;
            target.IsDisabled = false;

            await _dispatcherService.InvokeAsync(() =>
            {
                _notificationService.ShowSuccess(
                    $"✅ تم إعادة إنشاء الكرت ({target.Username}) بنجاح بالباقة ({selectedProfile}).",
                    "إعادة إنشاء الكرت");
            });

            await RefreshCurrentQueryAsync();
        }, $"جارٍ إعادة إنشاء الكرت ({target.Username})...");
    }
}
