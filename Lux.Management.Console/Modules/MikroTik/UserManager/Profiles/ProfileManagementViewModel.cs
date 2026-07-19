using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using System.Windows;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Profiles.ViewModels;

/// <summary>
/// Display model for the profiles DataGrid — holds computed/display values.
/// </summary>
public class ProfileModel : ObservableObject
{
    public Guid Id { get; set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string _systemType = string.Empty;
    public string SystemType
    {
        get => _systemType;
        set => SetProperty(ref _systemType, value);
    }

    /// <summary>سرعة الاتصال — e.g. "2M/2M"</summary>
    private string _speed = string.Empty;
    public string Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    /// <summary>حجم البيانات — human-readable display, e.g. "5 GB" or "500 MB"</summary>
    private string _size = string.Empty;
    public string Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    /// <summary>مدة الصلاحية — human-readable display, e.g. "30d" or "24h"</summary>
    private string _validity = string.Empty;
    public string Validity
    {
        get => _validity;
        set => SetProperty(ref _validity, value);
    }

    /// <summary>وقت الاستخدام الأقصى — e.g. "24h"</summary>
    private string _uptime = string.Empty;
    public string Uptime
    {
        get => _uptime;
        set => SetProperty(ref _uptime, value);
    }

    /// <summary>عدد الأجهزة المسموح بها — e.g. "1"</summary>
    private string _sharedUsers = "1";
    public string SharedUsers
    {
        get => _sharedUsers;
        set => SetProperty(ref _sharedUsers, value);
    }

    private decimal _sellingPrice;
    public decimal SellingPrice
    {
        get => _sellingPrice;
        set => SetProperty(ref _sellingPrice, value);
    }

    private decimal _agentPrice;
    public decimal AgentPrice
    {
        get => _agentPrice;
        set => SetProperty(ref _agentPrice, value);
    }

    private decimal _commission;
    public decimal Commission
    {
        get => _commission;
        set => SetProperty(ref _commission, value);
    }

    private int _linkedVouchers;
    public int LinkedVouchers
    {
        get => _linkedVouchers;
        set => SetProperty(ref _linkedVouchers, value);
    }

    private string _status = "نشطة";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}

public partial class ProfileManagementViewModel : ViewModelBase, IActivatable
{
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly IProfileService _profileService;
    private readonly IProfileCacheService _profileCacheService;
    private readonly MikroTikVoucherPrinter.Domain.Interfaces.Platform.IActiveRouterContext _activeRouterContext;

    // ── Collection & View ──────────────────────────────────────────────────────
    public ObservableCollection<ProfileModel> Profiles { get; } = new();
    private ICollectionView _profilesView;
    public ICollectionView ProfilesView => _profilesView;

    // ── Package Source ──────────────────────────────────────────────────────────
    private PackageSourceType _selectedSource;
    public PackageSourceType SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                Application.Current.Properties["LastProfileSource"] = value;
                // Force UI update for visibility properties
                OnPropertyChanged(nameof(IsUserManagerSource));
                OnPropertyChanged(nameof(IsHotspotSource));
                _ = LoadProfilesAsync(CancellationToken.None);
            }
        }
    }

    public List<PackageSourceType> AvailableSources { get; } = new()
    {
        PackageSourceType.UserManager,
        PackageSourceType.Hotspot
    };

    public bool IsUserManagerSource => SelectedSource == PackageSourceType.UserManager;
    public bool IsHotspotSource => SelectedSource == PackageSourceType.Hotspot;

    // ── Statistics Cards ───────────────────────────────────────────────────────
    [ObservableProperty] private int _totalProfilesCount;
    [ObservableProperty] private int _activeProfilesCount;
    [ObservableProperty] private int _hiddenProfilesCount;
    [ObservableProperty] private decimal _averagePrice;
    [ObservableProperty] private int _totalLinkedVouchersCount;

    [ObservableProperty]
    private WorkspaceState _currentState = WorkspaceState.Loading;

    // ── Filters ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _filterStatus = "All";

    partial void OnSearchTextChanged(string value) => _profilesView?.Refresh();
    partial void OnFilterStatusChanged(string value) => _profilesView?.Refresh();

    // ── Selection System ───────────────────────────────────────────────────────
    public ObservableCollection<string> SelectedProfileNames { get; } = new();
    public ObservableCollection<Guid> SelectedProfileIds { get; } = new();

    [ObservableProperty]
    private int _selectedCount;

    partial void OnSelectedCountChanged(int value)
    {
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ExportSelectedCommand.NotifyCanExecuteChanged();
    }

    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand ExportSelectedCommand { get; }

    // ── Dialog State ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isDialogOpen;
    [ObservableProperty] private string _dialogTitle = "إضافة باقة جديدة";
    [ObservableProperty] private ProfileModel _editingProfile = new();

    private bool _isEditMode;

    // ── Structured Dialog Input Fields (migrated from legacy) ──────────────────

    /// <summary>اسم الباقة — يُغلق في وضع التعديل</summary>
    [ObservableProperty] private bool _isNameEnabled = true;

    /// <summary>مدة الصلاحية بالأيام — يُحوَّل إلى "{n}d"</summary>
    [ObservableProperty] private int _durationDays = 30;

    /// <summary>قيمة حجم البيانات — يُجمع مع الوحدة ويُحوَّل لـ Bytes</summary>
    [ObservableProperty] private int _transferValue = 1;

    /// <summary>وحدة حجم البيانات — "MB" أو "GB"</summary>
    [ObservableProperty] private string _transferUnit = "GB";

    /// <summary>وقت الاستخدام بالساعات — يُحوَّل إلى "{n}h"، صفر = غير محدد</summary>
    [ObservableProperty] private int _uptimeHours = 0;

    /// <summary>سرعة الاتصال — نص حر مثل "2M/2M"</summary>
    [ObservableProperty] private string _rateLimit = string.Empty;

    /// <summary>عدد الأجهزة المسموح بها</summary>
    [ObservableProperty] private int _sharedUsers = 1;

    /// <summary>سعر البيع</summary>
    [ObservableProperty] private decimal _sellingPrice = 1000;

    /// <summary>سعر الوكيل — محلي فقط، لا يُرسل للراوتر</summary>
    [ObservableProperty] private decimal _agentPrice = 0;

    /// <summary>العمولة — محلية فقط</summary>
    [ObservableProperty] private decimal _commission = 0;

    // ── Transfer unit list ─────────────────────────────────────────────────────
    public List<string> TransferUnits { get; } = new() { "MB", "GB" };

    // ── SharedUsers list ───────────────────────────────────────────────────────
    public List<int> SharedUsersList { get; } = new() { 1, 2, 3, 4, 5, 10 };

    // ── Constructor ────────────────────────────────────────────────────────────
    public ProfileManagementViewModel(
        IPermissionService permissionService,
        IEventBus eventBus,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        INotificationService notificationService,
        IProfileService profileService,
        IProfileCacheService profileCacheService,
        MikroTikVoucherPrinter.Domain.Interfaces.Platform.IActiveRouterContext activeRouterContext) : base(permissionService, eventBus)
    {
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _profileService = profileService;
        _profileCacheService = profileCacheService;
        _activeRouterContext = activeRouterContext;

        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedCount > 0);
        ExportSelectedCommand = new AsyncRelayCommand(ExportSelectedAsync, () => SelectedCount > 0);

        Title = "إدارة الباقات والأسعار";

        _profilesView = CollectionViewSource.GetDefaultView(Profiles);
        _profilesView.Filter = FilterProfile;

        // Restore last selected source or default to UserManager
        if (Application.Current.Properties.Contains("LastProfileSource") && 
            Application.Current.Properties["LastProfileSource"] is PackageSourceType savedSource)
        {
            _selectedSource = savedSource;
        }
        else
        {
            _selectedSource = PackageSourceType.UserManager;
        }

        _ = LoadProfilesAsync(CancellationToken.None);
    }

    // ── Load ───────────────────────────────────────────────────────────────────
    // [PHASE-2] IActivatable.ActivateAsync — Lazy Loading عند التنقل
    public async Task ActivateAsync()
    {
        using var cts = new System.Threading.CancellationTokenSource();
        await LoadProfilesAsync(cts.Token);
    }

    private async Task LoadProfilesInternalAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var activeRouter = _activeRouterContext.CurrentRouter;
        if (activeRouter == null)
        {
            CurrentState = WorkspaceState.Error;
            _notificationService.ShowError("لا يوجد راوتر نشط حالياً.");
            return;
        }

        var host = activeRouter.Host;

        // 1. Check if cache is valid and we are not forcing refresh
        var cacheValid = await _profileCacheService.IsCacheValidAsync(host, SelectedSource, cancellationToken);
        if (cacheValid && !forceRefresh)
        {
            var cached = await _profileCacheService.GetCachedProfilesAsync(host, SelectedSource, cancellationToken);
            if (cached != null)
            {
                await PopulateProfilesListAsync(cached);
                CurrentState = cached.Count == 0 ? WorkspaceState.Empty : WorkspaceState.Loaded;
                return;
            }
        }

        // 2. Determine state: if we have cached data but it's expired, show it and transition to Refreshing
        var oldCached = await _profileCacheService.GetCachedProfilesAsync(host, SelectedSource, cancellationToken);
        if (oldCached != null && oldCached.Count > 0)
        {
            await PopulateProfilesListAsync(oldCached);
            CurrentState = WorkspaceState.Refreshing;
        }
        else
        {
            CurrentState = WorkspaceState.Loading;
        }

        // 3. Fetch from router in the background (Non-blocking UI)
        try
        {
            var liveProfiles = await _profileCacheService.FetchOrGetCachedAsync(
                host,
                SelectedSource,
                async (token) => await _profileService.GetAllProfilesAsync(SelectedSource, token),
                forceRefresh,
                cancellationToken);

            await PopulateProfilesListAsync(liveProfiles);
            CurrentState = liveProfiles.Count == 0 ? WorkspaceState.Empty : WorkspaceState.Loaded;
        }
        catch (Exception ex)
        {
            if (CurrentState == WorkspaceState.Loading)
            {
                CurrentState = WorkspaceState.Error;
            }
            else
            {
                CurrentState = WorkspaceState.Loaded;
                _notificationService.ShowError($"فشل تحديث الباقات: {ex.Message}");
            }
        }
    }

    private async Task PopulateProfilesListAsync(IReadOnlyList<Profile> list)
    {
        await _dispatcherService.InvokeAsync(() =>
        {
            Profiles.Clear();
            int skippedCount = 0;
            foreach (var p in list)
            {
                var model = MapToModel(p);
                if (model != null)
                {
                    Profiles.Add(model);
                }
                else
                {
                    skippedCount++;
                }
            }

            if (skippedCount > 0)
            {
                _notificationService.ShowWarning($"تم تجاهل {skippedCount} سجل تالف لعدم تحديد المصدر (SystemType).");
            }
            UpdateStatistics();
        });
    }

    [RelayCommand]
    private async Task LoadProfilesAsync(CancellationToken cancellationToken)
        => await LoadProfilesInternalAsync(false, cancellationToken);

    [RelayCommand]
    private async Task RefreshProfilesAsync(CancellationToken cancellationToken)
    {
        await _profileCacheService.ClearCacheAsync(cancellationToken);
        await LoadProfilesInternalAsync(true, cancellationToken);
    }

    // ── Filter ─────────────────────────────────────────────────────────────────
    private bool FilterProfile(object obj)
    {
        if (obj is not ProfileModel profile) return false;

        bool matchesSearch = string.IsNullOrWhiteSpace(SearchText) ||
                             profile.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

        bool matchesStatus = FilterStatus switch
        {
            "All" => true,
            "Active" => profile.Status == "نشطة",
            "Hidden" => profile.Status == "مخفية",
            "NoVouchers" => profile.LinkedVouchers == 0,
            _ => true
        };

        return matchesSearch && matchesStatus;
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedCount == 0) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            $"هل أنت متأكد من رغبتك في حذف الباقات المحددة ({SelectedCount} باقة) نهائياً من المايكروتك؟\nلا يمكن التراجع عن هذه الخطوة.",
            "تحذير: تأكيد حذف المحدد");
        if (!confirm) return;

        var namesToDelete = SelectedProfileNames.ToList();

        await ExecuteBusyAsync(async (ct) =>
        {
            int deleted = 0;
            int failed = 0;

            foreach (var name in namesToDelete)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await _profileService.DeleteProfileByNameAsync(SelectedSource, name, ct);
                    deleted++;
                }
                catch
                {
                    failed++;
                }
            }

            await _profileCacheService.ClearCacheAsync(ct);

            await _dispatcherService.InvokeAsync(() =>
            {
                var toRemove = Profiles.Where(p => namesToDelete.Contains(p.Name)).ToList();
                foreach (var p in toRemove) Profiles.Remove(p);

                SelectedProfileNames.Clear();
                SelectedProfileIds.Clear();
                SelectedCount = 0;

                UpdateStatistics();

                if (deleted > 0)
                {
                    _notificationService.ShowSuccess($"تم حذف {deleted} باقة بنجاح.");
                }
                if (failed > 0)
                {
                    _notificationService.ShowError($"فشل حذف {failed} باقة.");
                }
            });
        }, $"جاري حذف الباقات المحددة ({SelectedCount})...");
    }

    private async Task ExportSelectedAsync()
    {
        if (SelectedCount == 0) return;

        await ExecuteBusyAsync(async (ct) =>
        {
            await Task.Delay(500, ct); // Dummy logic
            await _dispatcherService.InvokeAsync(() =>
            {
                _notificationService.ShowSuccess($"تم تصدير {SelectedCount} باقة بنجاح (تجريبي).");
            });
        }, "جاري تصدير الباقات...");
    }

    // ── Dialog: Add ────────────────────────────────────────────────────────────
    [RelayCommand]
    private void ShowAddDialog()
    {
        DialogTitle = SelectedSource == PackageSourceType.UserManager ? "إضافة باقة User Manager" : "إضافة باقة Hotspot";
        _isEditMode = false;
        EditingProfile = new ProfileModel();

        // Reset all structured fields to defaults (same as legacy)
        ResetDialogFields();
        IsNameEnabled = true;
        IsDialogOpen = true;
    }

    // ── Dialog: Edit ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void ShowEditDialog(ProfileModel? profile)
    {
        if (profile == null) return;

        DialogTitle = SelectedSource == PackageSourceType.UserManager ? "تعديل باقة User Manager" : "تعديل باقة Hotspot";
        _isEditMode = true;

        // Clone display model for editing
        EditingProfile = new ProfileModel
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            SystemType = profile.SystemType,
            Speed = profile.Speed,
            Size = profile.Size,
            Validity = profile.Validity,
            Uptime = profile.Uptime,
            SharedUsers = profile.SharedUsers,
            SellingPrice = profile.SellingPrice,
            AgentPrice = profile.AgentPrice,
            Commission = profile.Commission,
            Status = profile.Status
        };

        // ── Parse stored MikroTik strings back to structured UI numbers ──────
        // Duration: "30d" → 30
        DurationDays = int.TryParse(profile.Validity?.Replace("d", "").Trim() ?? "", out int d) ? d : 0;

        // Transfer: "5 GB" or "500 MB" → value + unit
        // (ProfileService stores displayTransfer, not raw bytes, in Profile.Transfer for UI)
        if (!string.IsNullOrEmpty(profile.Size) && profile.Size.Contains(' '))
        {
            var parts = profile.Size.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int tv))
            {
                TransferValue = tv;
                TransferUnit = parts[1]; // "GB" or "MB"
            }
            else
            {
                TransferValue = 0;
                TransferUnit = "GB";
            }
        }
        else
        {
            TransferValue = 0;
            TransferUnit = "GB";
        }

        // Uptime: "24h" → 24
        UptimeHours = int.TryParse(profile.Uptime?.Replace("h", "").Trim() ?? "", out int h) ? h : 0;

        // RateLimit: freeform, no conversion needed
        RateLimit = profile.Speed ?? string.Empty;

        // SharedUsers: "1" → 1
        SharedUsers = int.TryParse(profile.SharedUsers, out int su) && su > 0 ? su : 1;

        // Pricing
        SellingPrice = profile.SellingPrice;
        AgentPrice = profile.AgentPrice;
        Commission = profile.Commission;

        // Lock name in edit mode — name is the MikroTik primary key
        IsNameEnabled = false;

        IsDialogOpen = true;
    }

    // ── Dialog: Copy ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void CopyProfile(ProfileModel? profile)
    {
        if (profile == null) return;

        DialogTitle = SelectedSource == PackageSourceType.UserManager ? "إضافة باقة User Manager" : "إضافة باقة Hotspot";
        _isEditMode = false;
        EditingProfile = new ProfileModel
        {
            Name = profile.Name + " (نسخة)",
            Description = profile.Description,
            SystemType = profile.SystemType,
            Speed = profile.Speed,
            Size = profile.Size,
            Validity = profile.Validity,
            Uptime = profile.Uptime,
            SharedUsers = profile.SharedUsers,
            SellingPrice = profile.SellingPrice,
            AgentPrice = profile.AgentPrice,
            Commission = profile.Commission,
            Status = profile.Status
        };

        // Parse-back same as edit (user starts from copied values)
        DurationDays = int.TryParse(profile.Validity?.Replace("d", "").Trim() ?? "", out int d) ? d : 0;
        if (!string.IsNullOrEmpty(profile.Size) && profile.Size.Contains(' '))
        {
            var parts = profile.Size.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int tv))
            { TransferValue = tv; TransferUnit = parts[1]; }
            else { TransferValue = 0; TransferUnit = "GB"; }
        }
        else { TransferValue = 0; TransferUnit = "GB"; }

        UptimeHours = int.TryParse(profile.Uptime?.Replace("h", "").Trim() ?? "", out int h) ? h : 0;
        RateLimit = profile.Speed ?? string.Empty;
        SharedUsers = int.TryParse(profile.SharedUsers, out int su) && su > 0 ? su : 1;
        SellingPrice = profile.SellingPrice;
        AgentPrice = profile.AgentPrice;
        Commission = profile.Commission;

        IsNameEnabled = true; // New profile — name is editable
        IsDialogOpen = true;
    }

    // ── Dialog: Close ──────────────────────────────────────────────────────────
    [RelayCommand]
    private void CloseDialog()
    {
        IsDialogOpen = false;
    }

    // ── Save (Create / Update) ─────────────────────────────────────────────────
    [RelayCommand]
    private async Task SaveProfileAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(EditingProfile.Name))
        {
            _notificationService.ShowError("يجب إدخال اسم الباقة");
            return;
        }

        await ExecuteBusyAsync(async (ct) =>
        {
            try
            {
                // ── Build MikroTik-format strings (exact legacy conversion) ──

                // Duration: days → "30d"
                string duration = DurationDays > 0 ? $"{DurationDays}d" : "";

                // Transfer: convert to raw bytes for RouterOS
                //           keep human-readable string for local UI display
                string transfer = "";
                string displayTransfer = "";
                if (TransferValue > 0)
                {
                    long bytes = TransferUnit == "GB"
                        ? (long)TransferValue * 1024 * 1024 * 1024
                        : (long)TransferValue * 1024 * 1024;
                    transfer = bytes.ToString();                          // raw bytes → MikroTik
                    displayTransfer = $"{TransferValue} {TransferUnit}"; // human text → UI
                }

                // Uptime: hours → "24h"
                string uptime = UptimeHours > 0 ? $"{UptimeHours}h" : "";

                // SharedUsers
                string sharedUsers = SharedUsers.ToString();

                if (!_isEditMode)
                {
                    // ── CREATE ──────────────────────────────────────────────
                    var created = await _profileService.CreateProfileAsync(
                        sourceType: SelectedSource,
                        name: EditingProfile.Name,
                        validity: duration,
                        transfer: transfer,     // raw bytes
                        uptime: uptime,
                        rateLimit: RateLimit,
                        sharedUsers: sharedUsers,
                        price: SellingPrice,
                        cancellationToken: ct);

                    await _dispatcherService.InvokeAsync(() =>
                    {
                        var model = MapToModel(created);
                        // Override raw bytes with human-readable display for the grid
                        model.Size = displayTransfer;
                        model.AgentPrice = AgentPrice;
                        model.Commission = Commission;
                        Profiles.Add(model);
                        UpdateStatistics();
                        _notificationService.ShowSuccess($"تمت إضافة الباقة [{EditingProfile.Name}] بنجاح وحقنها في المايكروتك");
                        IsDialogOpen = false;
                    });
                }
                else
                {
                    // ── UPDATE ──────────────────────────────────────────────
                    await _profileService.UpdateProfileAsync(
                        sourceType: SelectedSource,
                        name: EditingProfile.Name,
                        validity: duration,
                        transfer: transfer,     // raw bytes
                        uptime: uptime,
                        sharedUsers: sharedUsers,
                        price: SellingPrice,
                        cancellationToken: ct);

                    await _dispatcherService.InvokeAsync(() =>
                    {
                        var existing = Profiles.FirstOrDefault(p => p.Id == EditingProfile.Id);
                        if (existing != null)
                        {
                            existing.Validity = duration;
                            existing.Size = displayTransfer;     // human-readable
                            existing.Uptime = uptime;
                            existing.Speed = RateLimit;
                            existing.SharedUsers = sharedUsers;
                            existing.SellingPrice = SellingPrice;
                            existing.AgentPrice = AgentPrice;
                            existing.Commission = Commission;
                            existing.Description = EditingProfile.Description;
                            existing.Status = EditingProfile.Status;

                            // Force DataGrid refresh
                            int i = Profiles.IndexOf(existing);
                            Profiles.RemoveAt(i);
                            Profiles.Insert(i, existing);
                        }
                        UpdateStatistics();
                        _notificationService.ShowSuccess($"تم تعديل بيانات الباقة [{EditingProfile.Name}] بنجاح");
                        IsDialogOpen = false;
                    });
                }
            }
            catch (Exception ex)
            {
                await _dispatcherService.InvokeAsync(() =>
                    _notificationService.ShowError($"خطأ أثناء حفظ الباقة: {ex.Message}"));
            }
        }, _isEditMode ? "جاري حفظ التعديلات في المايكروتك..." : "جاري إنشاء الباقة في المايكروتك...");
    }

    // ── Delete ─────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task DeleteProfileAsync(ProfileModel? profile, CancellationToken cancellationToken)
    {
        if (profile == null) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            $"هل أنت متأكد من رغبتك في حذف الباقة نهائياً [{profile.Name}]؟\nلا يمكن التراجع عن هذه الخطوة.",
            "تحذير: تأكيد الحذف");
        if (!confirm) return;

        await ExecuteBusyAsync(async (ct) =>
        {
            try
            {
                await _profileService.DeleteProfileByNameAsync(SelectedSource, profile.Name, ct);
                await _dispatcherService.InvokeAsync(() =>
                {
                    Profiles.Remove(profile);
                    UpdateStatistics();
                    _notificationService.ShowSuccess($"تم حذف الباقة [{profile.Name}] بنجاح");
                });
            }
            catch (Exception ex)
            {
                await _dispatcherService.InvokeAsync(() =>
                    _notificationService.ShowError($"فشل حذف الباقة: {ex.Message}"));
            }
        }, "جاري الحذف من المايكروتك...");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ProfileModel? MapToModel(Profile p) 
    {
        if (string.IsNullOrEmpty(p.SystemType)) return null;

        return new()
        {
            Id = p.Id,
            Name = p.Name,
            Validity = p.Duration,
            Size = p.Transfer,       // stored as "5 GB" (displayTransfer) after CreateProfileAsync
            Uptime = p.Uptime,
            Speed = p.RateLimit,
            SharedUsers = p.SharedUsers,
            SellingPrice = p.Price,
            SystemType = p.SystemType,
            Status = "نشطة"
        };
    }

    private void ResetDialogFields()
    {
        DurationDays = 30;
        TransferValue = 1;
        TransferUnit = "GB";
        UptimeHours = 0;
        RateLimit = string.Empty;
        SharedUsers = 1;
        SellingPrice = 1000;
        AgentPrice = 0;
        Commission = 0;
    }

    private void UpdateStatistics()
    {
        TotalProfilesCount = Profiles.Count;
        ActiveProfilesCount = Profiles.Count(p => p.Status == "نشطة");
        HiddenProfilesCount = Profiles.Count(p => p.Status == "مخفية");
        AveragePrice = Profiles.Any() ? Profiles.Average(p => p.SellingPrice) : 0;
        TotalLinkedVouchersCount = Profiles.Sum(p => p.LinkedVouchers);
    }
}
