using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using System.Windows;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Services;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

public partial class VoucherManagementViewModel : BaseViewModel
{
    private const int UiChunkSize = 150;
    private const string AllProfilesOption = "ط¸ئ’ط¸â€‍ ط·آ§ط¸â€‍ط·آ¨ط·آ§ط¸â€ڑط·آ§ط·ع¾";
    private const string FilterInsightAll = "All";
    private readonly IVoucherQueryService _queryService;
    private readonly ISyncService _syncService;
    private readonly IPrintService _printService;
    private readonly IVoucherRepository _voucherRepo;
    private readonly ISettingsService _settingsService;

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ§ط¸â€‍ط·آ¨ط¸ظ¹ط·آ§ط¸â€ ط·آ§ط·ع¾
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    public ObservableCollection<VoucherDto> Vouchers { get; } = new();

    /// <summary>
    /// ط·آ§ط¸â€‍ط·آ¹ط·آ±ط·آ¶ ط·آ§ط¸â€‍ط¸â€¦ط¸ظ¾ط¸â€‍ط·ع¾ط·آ± أ¢â‚¬â€‌ ط¸ظ¹ط¸عˆط·آ³ط·ع¾ط·آ®ط·آ¯ط¸â€¦ ط¸ظ¾ط¸ظ¹ DataGrid ط¸â€¦ط·آ¨ط·آ§ط·آ´ط·آ±ط·آ©
    /// </summary>
    public ICollectionView VouchersView { get; }

    /// <summary>
    /// ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط¸ث†ط·ع¾ ط·آ§ط¸â€‍ط¸â€¦ط·آ­ط·آ¯ط·آ¯ط·آ© ط¸â€¦ط¸â€  ط·آ§ط¸â€‍ط¸â‚¬ DataGrid (ط·ع¾ط¸عˆط¸â€¦ط¸â€‍ط·آ£ ط¸â€¦ط¸â€  code-behind)
    /// </summary>
    public HashSet<Guid> SelectedVoucherIds { get; } = new();

    public VoucherManagementViewModel(
        IVoucherQueryService queryService,
        ISyncService syncService,
        IPrintService printService,
        IVoucherRepository voucherRepo,
        ISettingsService settingsService,
        ILogger<VoucherManagementViewModel> logger) : base(logger)
    {
        _queryService = queryService;
        _syncService  = syncService;
        _printService  = printService;
        _voucherRepo  = voucherRepo;
        _settingsService = settingsService;
        Title = "ط·آ¥ط·آ¯ط·آ§ط·آ±ط·آ© ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط¸ث†ط·ع¾";

        VouchersView = CollectionViewSource.GetDefaultView(Vouchers);
        VouchersView.Filter = FilterVouchers;

        LoadCommand          = new AsyncRelayCommand(LoadDataAsync);
        RetryFailedCommand   = new AsyncRelayCommand(RetryFailedDataAsync);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedCount > 0);
        PrintSelectedCommand  = new AsyncRelayCommand(PrintSelectedAsync,  () => SelectedCount > 0);
        ClearFilterCommand    = new RelayCommand(ClearFilters);
        ShowSessionsCommand   = new AsyncRelayCommand<VoucherDto>(ShowSessionsForVoucherAsync, v => v != null);
        CopyUsernameCommand   = new RelayCommand<VoucherDto>(CopyUsername, v => v != null && !string.IsNullOrEmpty(v.Username));
        CopyPasswordCommand   = new RelayCommand<VoucherDto>(CopyPassword, v => v != null);
    }

    public IAsyncRelayCommand<VoucherDto> ShowSessionsCommand { get; }
    public IRelayCommand<VoucherDto> CopyUsernameCommand { get; }
    public IRelayCommand<VoucherDto> CopyPasswordCommand { get; }

    private string _dataScopeSubtitle = "";
    public string DataScopeSubtitle
    {
        get => _dataScopeSubtitle;
        set => SetProperty(ref _dataScopeSubtitle, value);
    }

    private string _filterInsight = FilterInsightAll;
    public string FilterInsight
    {
        get => _filterInsight;
        set { SetProperty(ref _filterInsight, value); RefreshView(); }
    }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ§ط¸â€‍ط·آ¥ط·آ­ط·آµط·آ§ط·آ¦ط¸ظ¹ط·آ§ط·ع¾ ط·آ§ط¸â€‍ط·آ³ط·آ±ط¸ظ¹ط·آ¹ط·آ©
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private int _totalCount;
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }

    private int _syncedCount;
    public int SyncedCount { get => _syncedCount; set => SetProperty(ref _syncedCount, value); }

    private int _pendingCount;
    public int PendingCount { get => _pendingCount; set => SetProperty(ref _pendingCount, value); }

    private int _failedCount;
    public int FailedCount { get => _failedCount; set => SetProperty(ref _failedCount, value); }

    private int _filteredCount;
    public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ§ط¸â€‍ط¸ظ¾ط¸â€‍ط·آ§ط·ع¾ط·آ±
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { SetProperty(ref _searchText, value); RefreshView(); }
    }

    private string _filterStatus = "All";
    public string FilterStatus
    {
        get => _filterStatus;
        set { SetProperty(ref _filterStatus, value); RefreshView(); }
    }

    private string _filterSync = "All";
    public string FilterSync
    {
        get => _filterSync;
        set { SetProperty(ref _filterSync, value); RefreshView(); }
    }

    private string _filterProfile = AllProfilesOption;
    public string FilterProfile
    {
        get => _filterProfile;
        set { SetProperty(ref _filterProfile, value); RefreshView(); }
    }

    /// <summary>ط·آ¹ط·آ±ط·آ¶ ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط¸ث†ط·ع¾ ط·آ§ط¸â€‍ط·ع¾ط¸ظ¹ ط¸â€‍ط·آ§ ط·ع¾ط¸â€¦ط¸â€‍ط¸ئ’ ط·آ¨ط·آ±ط¸ث†ط¸ظ¾ط·آ§ط¸ظ¹ط¸â€‍ (ط¸ظ¾ط·آ§ط·آ±ط·ط› ط·آ£ط¸ث† ط¸â€ڑط¸ظ¹ط¸â€¦ط·آ© ط·آ´ط¸ئ’ط¸â€‍ط¸ظ¹ط·آ©).</summary>
    private bool _filterShowOnlyNoProfile;
    public bool FilterShowOnlyNoProfile
    {
        get => _filterShowOnlyNoProfile;
        set { SetProperty(ref _filterShowOnlyNoProfile, value); RefreshView(); }
    }

    /// <summary>ط·آ¹ط·آ±ط·آ¶ ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط¸ث†ط·ع¾ ط·آ§ط¸â€‍ط¸â€¦ط¸â€ ط·ع¾ط¸â€،ط¸ظ¹ط·آ© ط·آµط¸â€‍ط·آ§ط·آ­ط¸ظ¹ط·ع¾ط¸â€،ط·آ§ ط¸ظ¾ط¸â€ڑط·آ·.</summary>
    private bool _filterShowOnlyExpiredValidity;
    public bool FilterShowOnlyExpiredValidity
    {
        get => _filterShowOnlyExpiredValidity;
        set { SetProperty(ref _filterShowOnlyExpiredValidity, value); RefreshView(); }
    }

    /// <summary>ط·آ¹ط·آ±ط·آ¶ ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط¸ث†ط·ع¾ ط·آ§ط¸â€‍ط·ع¾ط¸ظ¹ ط¸â€ ط¸ظ¾ط·آ¯ ط·آ±ط·آµط¸ظ¹ط·آ¯ط¸â€،ط·آ§/ط·آ­ط·آµط·ع¾ط¸â€،ط·آ§ (ط·آ¨ط·آ§ط¸ظ¹ط·ع¾) ط¸ظ¾ط¸â€ڑط·آ·.</summary>
    private bool _filterShowOnlyQuotaDepleted;
    public bool FilterShowOnlyQuotaDepleted
    {
        get => _filterShowOnlyQuotaDepleted;
        set { SetProperty(ref _filterShowOnlyQuotaDepleted, value); RefreshView(); }
    }

    public ObservableCollection<string> ProfileFilters { get; } = new() { AllProfilesOption };

    private void RefreshView()
    {
        VouchersView.Refresh();
        FilteredCount = VouchersView.Cast<object>().Count();
    }

    private bool FilterVouchers(object obj)
    {
        if (obj is not VoucherDto v) return false;

        // ط¸ظ¾ط¸â€‍ط·ع¾ط·آ± ط·آ§ط¸â€‍ط¸â€ ط·آµ
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            bool matchText =
                v.Username.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                v.Profile.Contains(SearchText, StringComparison.OrdinalIgnoreCase)  ||
                v.BatchId.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase);

            if (!matchText) return false;
        }

        // ط¸ظ¾ط¸â€‍ط·ع¾ط·آ± ط·آ­ط·آ§ط¸â€‍ط·آ© ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط·ع¾
        if (FilterStatus != "All")
        {
            var statusMatch = FilterStatus switch
            {
                "Used"     => v.Status == VoucherStatus.Used,
                "Disabled" => v.IsDisabled,
                "Deleted"  => v.IsDeleted,
                _          => true
            };
            if (!statusMatch) return false;
        }

        // ط¸ظ¾ط¸â€‍ط·ع¾ط·آ± ط·آ­ط·آ§ط¸â€‍ط·آ© ط·آ§ط¸â€‍ط¸â€¦ط·آ²ط·آ§ط¸â€¦ط¸â€ ط·آ©
        if (FilterSync != "All")
        {
            var expectedSync = FilterSync switch
            {
                "Synced"  => SyncStatus.Synced,
                "Pending" => SyncStatus.Pending,
                "Failed"  => SyncStatus.Failed,
                _         => (SyncStatus?)null
            };
            if (expectedSync.HasValue && v.SyncStatus != expectedSync.Value) return false;
        }

        // ط¸ظ¾ط¸â€‍ط·ع¾ط·آ± ط·آ§ط¸â€‍ط·آ¨ط·آ§ط¸â€ڑط·آ©
        if (FilterProfile != AllProfilesOption &&
            !string.Equals(v.Profile, FilterProfile, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (FilterShowOnlyNoProfile && !v.HasNoProfile)
            return false;
        if (FilterShowOnlyExpiredValidity && v.Status != VoucherStatus.Expired)
            return false;
        if (FilterShowOnlyQuotaDepleted && !v.IsQuotaDepleted)
            return false;

        if (FilterInsight != FilterInsightAll)
        {
            var ok = FilterInsight switch
            {
                "LocalOnly" => v.DataOrigin == VoucherDataOrigin.Local && !v.IsDeleted,
                "RouterOnly" => v.DataOrigin == VoucherDataOrigin.RouterOnly,
                "RouterMerged" => v.DataOrigin == VoucherDataOrigin.RouterMerged,
                "HideDeleted" => !v.IsDeleted,
                _ => true
            };
            if (!ok) return false;
        }

        return true;
    }

    private void ClearFilters()
    {
        SearchText    = "";
        FilterStatus  = "All";
        FilterSync    = "All";
        FilterProfile = AllProfilesOption;
        FilterInsight = FilterInsightAll;
        FilterShowOnlyNoProfile = false;
        FilterShowOnlyExpiredValidity = false;
        FilterShowOnlyQuotaDepleted = false;
    }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ§ط¸â€‍ط·ع¾ط·آ­ط·آ¯ط¸ظ¹ط·آ¯
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private VoucherDto? _selectedVoucher;
    public VoucherDto? SelectedVoucher
    {
        get => _selectedVoucher;
        set => SetProperty(ref _selectedVoucher, value);
    }

    private int _selectedCount;
    public int SelectedCount
    {
        get => _selectedCount;
        set
        {
            SetProperty(ref _selectedCount, value);
            (DeleteSelectedCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (PrintSelectedCommand  as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
    }

    private bool _isAllSelected;
    public bool IsAllSelected
    {
        get => _isAllSelected;
        set => SetProperty(ref _isAllSelected, value);
    }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ±ط·آ³ط·آ§ط¸â€‍ط·آ© ط·آ¢ط·آ®ط·آ± ط·آ¹ط¸â€¦ط¸â€‍ط¸ظ¹ط·آ©
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private string _lastOperationMessage = "";
    public string LastOperationMessage
    {
        get => _lastOperationMessage;
        set => SetProperty(ref _lastOperationMessage, value);
    }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ§ط¸â€‍ط·آ£ط¸ث†ط·آ§ط¸â€¦ط·آ±
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    public IAsyncRelayCommand LoadCommand          { get; }
    public IAsyncRelayCommand RetryFailedCommand   { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand PrintSelectedCommand  { get; }
    public IRelayCommand      ClearFilterCommand    { get; }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ§ط¸â€‍ط·ع¾ط¸â€،ط¸ظ¹ط·آ¦ط·آ©
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private bool _isInitialized;
    
    public override async Task InitializeAsync(object? parameter = null)
    {
        var host = _settingsService.Get("MikroTik.Host", "--");
        DataScopeSubtitle = $"البيانات مرتبطة بالراوتر الحالي ({host})";

        if (!_isInitialized)
        {
            await LoadDataAsync();
            _isInitialized = true;
        }
    }

    private async Task ShowSessionsForVoucherAsync(VoucherDto? v)
    {
        if (v == null) return;
        await ExecuteBusyAsync(async (token) =>
        {
            var lines = await _queryService.GetHotspotActiveSessionsForUserAsync(v.Username, token);
            var text = string.Join(Environment.NewLine, lines);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(text, $"ط·آ¬ط¸â€‍ط·آ³ط·آ§ط·ع¾: {v.Username}", MessageBoxButton.OK, MessageBoxImage.Information,
                    MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign));
        }, "ط·آ¬ط·آ§ط·آ±ط¸ظ¹ ط·آ¬ط¸â€‍ط·آ¨ ط·آ§ط¸â€‍ط·آ¬ط¸â€‍ط·آ³ط·آ§ط·ع¾ ط·آ§ط¸â€‍ط¸â€ ط·آ´ط·آ·ط·آ©...");
    }

    private static void CopyUsername(VoucherDto? v)
    {
        if (v == null || string.IsNullOrEmpty(v.Username)) return;
        try { Clipboard.SetText(v.Username); } catch { /* ignore */ }
    }

    private static void CopyPassword(VoucherDto? v)
    {
        if (v == null) return;
        try { Clipboard.SetText(v.Password ?? ""); } catch { /* ignore */ }
    }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ¬ط¸â€‍ط·آ¨ ط·آ§ط¸â€‍ط·آ¨ط¸ظ¹ط·آ§ط¸â€ ط·آ§ط·ع¾
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private async Task LoadDataAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            IReadOnlyList<VoucherDto> data;
            try
            {
                data = await _queryService.GetAllVouchersFromMikroTikAsync(token);
            }
            catch
            {
                // fallback ط·آ³ط·آ±ط¸ظ¹ط·آ¹ ط¸ظ¾ط¸ظ¹ ط·آ­ط·آ§ط¸â€‍ ط·ع¾ط·آ¹ط·آ°ط·آ± ط·آ§ط¸â€‍ط·آ§ط·ع¾ط·آµط·آ§ط¸â€‍: ط¸â€ ط·آ¹ط·آ±ط·آ¶ ط·آ§ط¸â€‍ط¸â€¦ط·آ­ط¸â€‍ط¸ظ¹ ط¸ظ¾ط¸ث†ط·آ±ط·آ§ط¸â€¹
                data = await _queryService.GetAllVouchersAsync(token);
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TotalCount   = data.Count;
                SyncedCount  = data.Count(x => x.SyncStatus == SyncStatus.Synced);
                PendingCount = data.Count(x => x.SyncStatus == SyncStatus.Pending);
                FailedCount  = data.Count(x => x.SyncStatus == SyncStatus.Failed);
            });

            await ReplaceVouchersIncrementalAsync(data, token);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => UpdateProfileFilters(data));

            Logger.LogInformation("أ¢إ“â€¦ ط·ع¾ط¸â€¦ ط·ع¾ط·آ­ط¸â€¦ط¸ظ¹ط¸â€‍ {Count} ط¸ئ’ط·آ±ط·ع¾ (ط¸â€¦ط·آµط·آ¯ط·آ±: MikroTik/Local fallback)", data.Count);

        }, "ط·آ¬ط·آ§ط·آ±ط¸ظ¹ ط·آ¬ط¸â€‍ط·آ¨ ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط¸ث†ط·ع¾...");
    }

    private void UpdateProfileFilters(IReadOnlyList<VoucherDto> data)
    {
        var profiles = data.Select(x => x.Profile)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ProfileFilters.Clear();
        ProfileFilters.Add(AllProfilesOption);
        foreach (var profile in profiles)
        {
            ProfileFilters.Add(profile);
        }

        if (!ProfileFilters.Contains(FilterProfile))
        {
            FilterProfile = AllProfilesOption;
        }
    }

    private async Task ReplaceVouchersIncrementalAsync(IReadOnlyList<VoucherDto> data, CancellationToken token)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Vouchers.Clear();
        });

        for (int i = 0; i < data.Count; i += UiChunkSize)
        {
            token.ThrowIfCancellationRequested();
            var chunk = data.Skip(i).Take(UiChunkSize).ToList();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var item in chunk)
                {
                    Vouchers.Add(item);
                }
            });

            await Task.Yield();
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            RefreshView();
        });
    }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ¥ط·آ¹ط·آ§ط·آ¯ط·آ© ط¸â€¦ط·آ­ط·آ§ط¸ث†ط¸â€‍ط·آ© ط·آ§ط¸â€‍ط¸ظ¾ط·آ§ط·آ´ط¸â€‍ط·آ©
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private async Task RetryFailedDataAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var metrics = await _syncService.RetryFailedAsync(token);
            LastOperationMessage = $"أ¢إ“â€¦ ط·ع¾ط¸â€¦ط·ع¾ ط·آ¥ط·آ¹ط·آ§ط·آ¯ط·آ© ط·آ§ط¸â€‍ط¸â€¦ط·آ­ط·آ§ط¸ث†ط¸â€‍ط·آ© أ¢â‚¬â€‌ ط¸â€ ط·آ¬ط·آ­: {metrics.Success} | ط¸ظ¾ط·آ´ط¸â€‍: {metrics.Failed}";
            Logger.LogInformation("ظ‹ع؛â€‌â€‍ ط·آ¥ط·آ¹ط·آ§ط·آ¯ط·آ© ط·آ§ط¸â€‍ط¸â€¦ط·آ²ط·آ§ط¸â€¦ط¸â€ ط·آ©: {Metrics}", metrics.ToString());
            await LoadDataAsync();

        }, "ط·آ¬ط·آ§ط·آ±ط¸ظ¹ ط·آ¥ط·آ¹ط·آ§ط·آ¯ط·آ© ط¸â€¦ط·آ¹ط·آ§ط¸â€‍ط·آ¬ط·آ© ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط¸ث†ط·ع¾ ط·آ§ط¸â€‍ط¸ظ¾ط·آ§ط·آ´ط¸â€‍ط·آ©...");
    }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ­ط·آ°ط¸ظ¾ ط·آ§ط¸â€‍ط¸â€¦ط·آ­ط·آ¯ط·آ¯ (soft delete ط¸â€¦ط¸â€  ط¸â€ڑط·آ§ط·آ¹ط·آ¯ط·آ© ط·آ§ط¸â€‍ط·آ¨ط¸ظ¹ط·آ§ط¸â€ ط·آ§ط·ع¾ ط·آ§ط¸â€‍ط¸â€¦ط·آ­ط¸â€‍ط¸ظ¹ط·آ©)
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private async Task DeleteSelectedAsync()
    {
        if (SelectedCount == 0) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var selectedIds = SelectedVoucherIds.ToList();
            int deleted = 0;
            int failed  = 0;

            foreach (var id in selectedIds)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var entity = await _voucherRepo.GetAsync(id, token);
                    if (entity != null)
                    {
                        await _voucherRepo.SoftDeleteAsync(entity, token);
                        deleted++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Logger.LogError(ex, "ط¸ظ¾ط·آ´ط¸â€‍ ط·آ­ط·آ°ط¸ظ¾ ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط·ع¾ {Id}", id);
                }
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // ط·آ¥ط·آ²ط·آ§ط¸â€‍ط·آ© ط¸â€¦ط¸â€  ط·آ§ط¸â€‍ط¸ئ’ط¸ث†ط¸â€‍ط¸ئ’ط·آ´ط¸â€  ط·آ§ط¸â€‍ط¸â€¦ط·آ±ط·آ¦ط¸ظ¹ط·آ©
                var toRemove = Vouchers.Where(v => selectedIds.Contains(v.Id)).ToList();
                foreach (var v in toRemove) Vouchers.Remove(v);
                SelectedVoucherIds.Clear();
                SelectedCount = 0;
                RefreshView();
            });

            LastOperationMessage = deleted > 0
                ? $"أ¢إ“â€¦ ط·ع¾ط¸â€¦ ط·آ­ط·آ°ط¸ظ¾ {deleted} ط¸ئ’ط·آ±ط·ع¾ ط·آ¨ط¸â€ ط·آ¬ط·آ§ط·آ­" + (failed > 0 ? $" | ط¸ظ¾ط·آ´ط¸â€‍ {failed}" : "")
                : $"أ¢â€Œإ’ ط¸ظ¾ط·آ´ط¸â€‍ ط·آ§ط¸â€‍ط·آ­ط·آ°ط¸ظ¾ ({failed} ط¸ئ’ط·آ±ط·ع¾)";
            Logger.LogInformation("ط·آ­ط·آ°ط¸ظ¾: ط¸â€ ط·آ¬ط·آ­ {D} | ط¸ظ¾ط·آ´ط¸â€‍ {F}", deleted, failed);

        }, $"ط·آ¬ط·آ§ط·آ±ط¸ظ¹ ط·آ­ط·آ°ط¸ظ¾ {SelectedCount} ط¸ئ’ط·آ±ط·ع¾...");
    }

    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    //  ط·آ·ط·آ¨ط·آ§ط·آ¹ط·آ© ط·آ§ط¸â€‍ط¸â€¦ط·آ­ط·آ¯ط·آ¯
    // أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯أ¢â€¢ع¯
    private async Task PrintSelectedAsync()
    {
        if (SelectedCount == 0) return;

        // ط¸â€ ط·آ¬ط¸â€¦ط·آ¹ ط·آ§ط¸â€‍ط¸ئ’ط·آ±ط¸ث†ط·ع¾ ط·آ§ط¸â€‍ط¸â€¦ط·آ­ط·آ¯ط·آ¯ط·آ© ط¸â€¦ط¸â€  ط·آ§ط¸â€‍ط·آ¹ط·آ±ط·آ¶ (ط¸â€ ط·آ³ط·ع¾ط·آ®ط·آ¯ط¸â€¦ SelectedVoucher ط¸ئ’ط¸â€¦ط·آ¤ط·آ´ط·آ±)
        // ط·آ§ط¸â€‍ط·آ·ط·آ¨ط·آ§ط·آ¹ط·آ© ط·آ§ط¸â€‍ط¸ئ’ط·آ§ط¸â€¦ط¸â€‍ط·آ© ط¸â€‍ط¸â€‍ط¸â€¦ط·آ­ط·آ¯ط·آ¯ ط·ع¾ط·ع¾ط·آ·ط¸â€‍ط·آ¨ ط·ع¾ط¸â€¦ط·آ±ط¸ظ¹ط·آ± ط¸â€ڑط·آ§ط·آ¦ط¸â€¦ط·آ© ط¸â€¦ط¸â€  code-behind أ¢â‚¬â€‌ ط¸â€ ط·آ·ط·آ¨ط·آ¹ ط·آ§ط¸â€‍ط¸ئ’ط¸â€‍ ط¸â€¦ط·آ¤ط¸â€ڑط·ع¾ط·آ§ط¸â€¹
        await ExecuteBusyAsync(async (token) =>
        {
            var selected = Vouchers.Where(v => SelectedVoucherIds.Contains(v.Id)).ToList();
            if (selected.Count == 0) return;

            var settings = new PrintSettingsDto();
            var result = await _printService.GeneratePdfAsync(
                new System.Collections.Generic.List<VoucherDto>(selected), settings, null, token);

            if (result.IsSuccess)
            {
                string tempFile = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"luxcard_selected_{DateTime.Now:HHmmss}.pdf");
                System.IO.File.WriteAllBytes(tempFile, result.Value);
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(tempFile) { UseShellExecute = true });

                LastOperationMessage = $"ظ‹ع؛â€“آ¨أ¯آ¸عˆ ط·ع¾ط¸â€¦ ط¸ظ¾ط·ع¾ط·آ­ PDF ط¸â€‍ط¸â‚¬ {selected.Count} ط¸ئ’ط·آ±ط·ع¾";
            }
            else
            {
                LastOperationMessage = $"أ¢â€Œإ’ ط¸ظ¾ط·آ´ط¸â€‍ط·ع¾ ط·آ§ط¸â€‍ط·آ·ط·آ¨ط·آ§ط·آ¹ط·آ©: {result.ErrorMessage}";
                Logger.LogError("ط¸ظ¾ط·آ´ط¸â€‍ PDF: {Err}", result.ErrorMessage);
            }

        }, $"ط·آ¬ط·آ§ط·آ±ط¸ظ¹ ط·آ¥ط¸â€ ط·آ´ط·آ§ط·طŒ PDF ط¸â€‍ط¸â‚¬ {SelectedCount} ط¸ئ’ط·آ±ط·ع¾ ط¸â€¦ط·آ­ط·آ¯ط·آ¯...");
    }
}