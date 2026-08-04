using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;

using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Batches.ViewModels;

public partial class BatchManagementViewModel : ViewModelBase, IActivatable
{
    private readonly IBatchService _batchService;
    private readonly IBatchQueryService _batchQueryService;
    private readonly IVoucherRepository _voucherRepo;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly ILogger<BatchManagementViewModel> _logger;

    public BatchManagementViewModel(
        IBatchService batchService,
        IBatchQueryService batchQueryService,
        IVoucherRepository voucherRepo,
        IActiveRouterContext activeRouterContext,
        IPermissionService permissionService,
        IEventBus eventBus,
        ILogger<BatchManagementViewModel> logger) : base(permissionService, eventBus)
    {
        _batchService = batchService;
        _batchQueryService = batchQueryService;
        _voucherRepo = voucherRepo;
        _activeRouterContext = activeRouterContext;
        _logger = logger;

        Title = "إدارة الدفعات (Batch Management)";

        _activeRouterContext.ActiveRouterChanged += (s, e) => _ = LoadBatchesAsync();

        Batches = new ObservableCollection<BatchDto>();
        SelectedBatchVouchers = new ObservableCollection<VoucherDto>();

        LoadBatchesCommand        = new AsyncRelayCommand(LoadBatchesAsync);
        SyncBatchCommand          = new AsyncRelayCommand<BatchDto>(SyncBatchAsync);
        RetryFailedCommand        = new AsyncRelayCommand<BatchDto>(RetryFailedAsync);
        ResumeSyncCommand         = new AsyncRelayCommand<BatchDto>(ResumeSyncAsync);
        CancelSyncCommand         = new AsyncRelayCommand<BatchDto>(CancelSyncAsync);
        PrintBatchCommand         = new AsyncRelayCommand<BatchDto>(PrintBatchAsync);
        ReprintBatchCommand       = new AsyncRelayCommand<BatchDto>(ReprintBatchAsync);
        OpenPdfFolderCommand      = new AsyncRelayCommand<BatchDto>(OpenPdfFolderAsync);
        DeleteBatchCommand        = new AsyncRelayCommand<BatchDto>(DeleteBatchAsync);
        ArchiveBatchCommand       = new AsyncRelayCommand<BatchDto>(ArchiveBatchAsync);
        ViewBatchVouchersCommand  = new AsyncRelayCommand<BatchDto>(ViewBatchVouchersAsync);

        CopyCredentialsCommand     = new RelayCommand<VoucherDto>(CopyCredentials);
        DisableVoucherCommand      = new AsyncRelayCommand<VoucherDto>(DisableVoucherAsync);
        DeleteSingleVoucherCommand  = new AsyncRelayCommand<VoucherDto>(DeleteSingleVoucherAsync);
        ToggleVoucherPanelCommand  = new RelayCommand(() => IsVoucherPanelOpen = !IsVoucherPanelOpen);
    }

    public Task ActivateAsync() => LoadBatchesAsync();

    // ─── Observable Properties ──────────────────────────────────────────────────

    public ObservableCollection<BatchDto> Batches { get; }
    public ObservableCollection<VoucherDto> SelectedBatchVouchers { get; }

    private BatchDto? _selectedBatch;
    public BatchDto? SelectedBatch
    {
        get => _selectedBatch;
        set
        {
            if (SetProperty(ref _selectedBatch, value) && value is not null)
            {
                _ = ViewBatchVouchersAsync(value);
            }
        }
    }

    private BatchProgress? _currentProgress;
    public BatchProgress? CurrentProgress
    {
        get => _currentProgress;
        set => SetProperty(ref _currentProgress, value);
    }

    private bool _isVoucherPanelOpen;
    public bool IsVoucherPanelOpen
    {
        get => _isVoucherPanelOpen;
        set => SetProperty(ref _isVoucherPanelOpen, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterBatches();
            }
        }
    }

    private List<BatchDto> _allBatches = new();

    // ─── Commands ───────────────────────────────────────────────────────────────

    public IAsyncRelayCommand LoadBatchesCommand { get; }
    public IAsyncRelayCommand<BatchDto> SyncBatchCommand { get; }
    public IAsyncRelayCommand<BatchDto> RetryFailedCommand { get; }
    public IAsyncRelayCommand<BatchDto> ResumeSyncCommand { get; }
    public IAsyncRelayCommand<BatchDto> CancelSyncCommand { get; }
    public IAsyncRelayCommand<BatchDto> PrintBatchCommand { get; }
    public IAsyncRelayCommand<BatchDto> ReprintBatchCommand { get; }
    public IAsyncRelayCommand<BatchDto> OpenPdfFolderCommand { get; }
    public IAsyncRelayCommand<BatchDto> DeleteBatchCommand { get; }
    public IAsyncRelayCommand<BatchDto> ArchiveBatchCommand { get; }
    public IAsyncRelayCommand<BatchDto> ViewBatchVouchersCommand { get; }

    public IRelayCommand<VoucherDto> CopyCredentialsCommand { get; }
    public IAsyncRelayCommand<VoucherDto> DisableVoucherCommand { get; }
    public IAsyncRelayCommand<VoucherDto> DeleteSingleVoucherCommand { get; }
    public IRelayCommand ToggleVoucherPanelCommand { get; }

    // ─── Implementations ────────────────────────────────────────────────────────

    public async Task LoadBatchesAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var data = await _batchQueryService.GetAllBatchesAsync(token);
            _allBatches = data.ToList();
            FilterBatches();
        }, "جاري تحميل الدفعات...");
    }

    private void FilterBatches()
    {
        Batches.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allBatches
            : _allBatches.Where(b => b.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     b.ProfileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var batch in filtered)
        {
            Batches.Add(batch);
        }
    }

    private async Task SyncBatchAsync(BatchDto? batch)
    {
        if (batch is null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var progress = new Progress<BatchProgress>(p => CurrentProgress = p);
            await _batchService.SyncBatchAsync(batch.Id, progress, token);
            await RefreshSingleBatchAsync(batch.Id, token);
        }, $"جاري مزامنة الدفعة '{batch.Name}'...");
    }

    private async Task RetryFailedAsync(BatchDto? batch)
    {
        if (batch is null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var progress = new Progress<BatchProgress>(p => CurrentProgress = p);
            await _batchService.RetryFailedBatchAsync(batch.Id, progress, token);
            await RefreshSingleBatchAsync(batch.Id, token);
        }, $"جاري إعادة محاولة الكروت الفاشلة للدفعة '{batch.Name}'...");
    }

    private async Task ResumeSyncAsync(BatchDto? batch)
    {
        if (batch is null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var progress = new Progress<BatchProgress>(p => CurrentProgress = p);
            await _batchService.ResumeBatchAsync(batch.Id, progress, token);
            await RefreshSingleBatchAsync(batch.Id, token);
        }, $"جاري استكمال مزامنة الدفعة '{batch.Name}'...");
    }

    private async Task CancelSyncAsync(BatchDto? batch)
    {
        if (batch is null) return;

        await _batchService.CancelSyncAsync(batch.Id);
        await RefreshSingleBatchAsync(batch.Id);
    }

    private async Task PrintBatchAsync(BatchDto? batch)
    {
        if (batch is null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var result = await _batchService.PrintBatchAsync(batch.Id, null, token);
            if (result.IsSuccess)
            {
                MessageBox.Show($"تم توليد ملف PDF بنجاح!\nالمسار: {result.PdfPath}", "نجاح الطباعة", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"فشل توليد PDF: {result.ErrorMessage}", "خطأ في الطباعة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            await RefreshSingleBatchAsync(batch.Id, token);
        }, $"جاري توليد PDF للدفعة '{batch.Name}'...");
    }

    private async Task ReprintBatchAsync(BatchDto? batch)
    {
        if (batch is null) return;

        var confirm = MessageBox.Show($"هل تريد إعادة توليد PDF للدفعة '{batch.Name}'؟", "إعادة طباعة", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var result = await _batchService.ReprintBatchAsync(batch.Id, null, token);
            if (result.IsSuccess)
            {
                MessageBox.Show($"تم إعادة توليد ملف PDF بنجاح!\nالمسار: {result.PdfPath}", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            await RefreshSingleBatchAsync(batch.Id, token);
        }, $"جاري إعادة طباعة الدفعة '{batch.Name}'...");
    }

    private async Task OpenPdfFolderAsync(BatchDto? batch)
    {
        if (batch is null) return;
        var opened = await _batchService.OpenPdfFolderAsync(batch.Id);
        if (!opened)
        {
            MessageBox.Show("لم يتم العثور على مجلد PDF أو لم يُولّد ملف PDF لهذه الدفعة بعد.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteBatchAsync(BatchDto? batch)
    {
        if (batch is null) return;

        var result = MessageBox.Show(
            $"🚨 تحذير هام!\n\nهل أنت متأكد من حذف الدفعة '{batch.Name}' بالكامل مع جميع كروتها ({batch.TotalCards} كرت)؟\n\nلا يمكن التراجع عن هذا الإجراء!",
            "تأكيد حذف الدفعة",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        await ExecuteBusyAsync(async (token) =>
        {
            await _batchService.DeleteBatchAsync(batch.Id, token);
            Batches.Remove(batch);
            _allBatches.Remove(batch);
            if (SelectedBatch?.Id == batch.Id)
            {
                SelectedBatch = null;
                SelectedBatchVouchers.Clear();
                IsVoucherPanelOpen = false;
            }
        }, $"جاري حذف الدفعة '{batch.Name}'...");
    }

    private async Task ArchiveBatchAsync(BatchDto? batch)
    {
        if (batch is null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            await _batchService.ArchiveBatchAsync(batch.Id, token);
            await RefreshSingleBatchAsync(batch.Id, token);
        }, $"جاري أرشفة الدفعة '{batch.Name}'...");
    }

    private async Task ViewBatchVouchersAsync(BatchDto? batch)
    {
        if (batch is null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var vouchers = await _batchQueryService.GetBatchVouchersAsync(batch.Id, token);
            SelectedBatchVouchers.Clear();
            foreach (var v in vouchers)
            {
                SelectedBatchVouchers.Add(v);
            }
            IsVoucherPanelOpen = true;
        }, "جاري جلب كروت الدفعة...");
    }

    private void CopyCredentials(VoucherDto? voucher)
    {
        if (voucher is null) return;
        var text = $"المستخدم: {voucher.Username}\nكلمة السر: {voucher.Password}\nالباقة: {voucher.Profile}";
        Clipboard.SetText(text);
        MessageBox.Show("تم نسخ بيانات الكرت إلى الحافظة.", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task DisableVoucherAsync(VoucherDto? voucher)
    {
        if (voucher is null) return;
        var entity = await _voucherRepo.GetAsync(voucher.Id);
        if (entity is not null)
        {
            entity.IsDisabled = !entity.IsDisabled;
            await _voucherRepo.UpdateAsync(entity);
            voucher.IsDisabled = entity.IsDisabled;
        }
    }

    private async Task DeleteSingleVoucherAsync(VoucherDto? voucher)
    {
        if (voucher is null) return;

        var confirm = MessageBox.Show($"هل تريد حذف الكرت '{voucher.Username}'؟", "حذف كرت", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var entity = await _voucherRepo.GetAsync(voucher.Id);
        if (entity is not null)
        {
            await _voucherRepo.SoftDeleteAsync(entity);
            SelectedBatchVouchers.Remove(voucher);
        }
    }

    private async Task RefreshSingleBatchAsync(Guid batchId, System.Threading.CancellationToken token = default)
    {
        var updated = await _batchQueryService.GetBatchAsync(batchId, token);
        if (updated is null) return;

        var index = _allBatches.FindIndex(b => b.Id == batchId);
        if (index >= 0) _allBatches[index] = updated;

        var uiIndex = Batches.Select((b, i) => new { b.Id, Index = i }).FirstOrDefault(x => x.Id == batchId)?.Index ?? -1;
        if (uiIndex >= 0) Batches[uiIndex] = updated;

        if (SelectedBatch?.Id == batchId) SelectedBatch = updated;
    }
}
