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

    // إلغاء الاستعلامات الجارية
    private CancellationTokenSource? _queryCts;
    private DispatcherTimer? _debounceTimer;

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
    public IRelayCommand<VoucherDto> CopyUsernameCommand { get; }
    public IRelayCommand<VoucherDto> CopyPasswordCommand { get; }

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
        ILogger<VoucherManagementViewModel> logger,
        IFeatureAuthorizationService featureAuthorizationService) : base(permissionService, eventBus)
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
        _logger = logger;

        Title = "إدارة الكروت";

        // ربط الأوامر
        LoadCommand = new AsyncRelayCommand(InitializeAsync);
        RefreshCommand = new AsyncRelayCommand(InstantSyncAsync, () => !IsSyncingWithRouter);
        RetryFailedCommand = new AsyncRelayCommand(RetryFailedDataAsync);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedCount > 0);
        RestoreSelectedCommand = new AsyncRelayCommand(RestoreSelectedAsync, () => SelectedCount > 0);
        PrintSelectedCommand = new AsyncRelayCommand(PrintSelectedAsync, () => SelectedCount > 0);
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
        CopyUsernameCommand = new RelayCommand<VoucherDto>(CopyUsername, v => v != null && !string.IsNullOrEmpty(v.Username));
        CopyPasswordCommand = new RelayCommand<VoucherDto>(CopyPassword, v => v != null);

        // تهيئة المؤقت للـ Search Debouncing
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;

        // تفعيل استرداد الحالة عند الانتقال
        _stateTracker.HasSavedState = true;
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
                            Vouchers[i].SyncStatus != result.Items[i].SyncStatus)
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
    //  مزامنة الخلفية الذكية (Background Sync)
    // ══════════════════════════════════════════════════════
    private void TriggerBackgroundSync()
    {
        if (PendingSyncCount == 0) return;

        ShowStatusBanner(ScreenStatus.PendingChanges, $"توجد {PendingSyncCount} كروت بانتظار المزامنة مع الراوتر...");

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
                
                // بدء المزامنة
                var metrics = await _syncService.ProcessPendingAsync(progress, token);

                await _dispatcherService.InvokeAsync(async () =>
                {
                    await UpdateRouterStatusWidgetAsync();
                    
                    if (metrics.Failed > 0)
                    {
                        ShowStatusBanner(ScreenStatus.Failed, $"اكتملت المزامنة مع وجود أخطاء: نجح {metrics.Success} | فشل {metrics.Failed}");
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

    private async Task PrintSelectedAsync()
    {
        if (SelectedCount == 0) return;

        if (!_featureAuthorizationService.CanExecute(FeatureId.VoucherPrinting, SelectedCount))
        {
            _notificationService.ShowError($"لا يمكن طباعة أكثر من {SecurityConfiguration.MaxFreeVouchersLimit} كرت في النسخة المجانية. يرجى تفعيل البرنامج.");
            return;
        }

        await ExecuteBusyAsync(async (token) =>
        {
            // [C-1 FIX] استخدام _dbFactory المحقون بدلاً من ServiceProvider
            IReadOnlyList<VoucherDto> selected;
            var selectedIds = SelectedVoucherIds.ToList();

            using var db = await _dbFactory.CreateDbContextAsync(token);
            selected = await db.Vouchers
                .IgnoreQueryFilters()
                .Include(v => v.Agent)
                .AsNoTracking()
                .Where(v => selectedIds.Contains(v.Id))
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

            var settings = new PrintSettingsDto();
            var result = await _printService.GeneratePdfAsync(new List<VoucherDto>(selected), settings, token);

            if (result.IsSuccess)
            {
                string tempFileName = $"luxcard_selected_{DateTime.Now:HHmmss}.pdf";
                await _printPreviewService.PreviewPdfAsync(result.Value, tempFileName, token);
                _logger.LogInformation("تم فتح PDF لـ {Count} كرت", selected.Count);
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
                _featureAuthorizationService);
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
}
