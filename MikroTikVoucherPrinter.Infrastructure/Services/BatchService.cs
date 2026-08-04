using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// الخدمة المركزية لإدارة الدفعات.
/// تنسّق بين التوليد والمزامنة والطباعة وتُحدّث حالة الـ Batch في كل خطوة.
/// </summary>
public class BatchService : IBatchService
{
    private readonly IVoucherGenerationService _generationService;
    private readonly ISyncService              _syncService;
    private readonly IPrintService             _printService;
    private readonly IBatchRepository          _batchRepo;
    private readonly IBatchQueryService        _batchQueryService;
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ILogger<BatchService>     _logger;

    public BatchService(
        IVoucherGenerationService generationService,
        ISyncService              syncService,
        IPrintService             printService,
        IBatchRepository          batchRepo,
        IBatchQueryService        batchQueryService,
        IDbContextFactory<LuxCardDbContext> dbFactory,
        ILogger<BatchService>     logger)
    {
        _generationService = generationService;
        _syncService       = syncService;
        _printService      = printService;
        _batchRepo         = batchRepo;
        _batchQueryService = batchQueryService;
        _dbFactory         = dbFactory;
        _logger            = logger;
    }

    // ─── إنشاء ──────────────────────────────────────────────────────────────────

    public async Task<Guid> CreateBatchAsync(
        CreateBatchRequest request,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🆕 [Batch] إنشاء دفعة جديدة: {Name} ({Count} كرت)", request.Name, request.GenerationSettings.Count);

        // تحويل إلى VoucherGenerationRequest
        var genRequest = request.GenerationSettings;

        // إنشاء الكروت عبر الخدمة المخصصة
        var innerProgress = progress is null ? null :
            new Progress<(int success, int failed, int total, string phase)>(r =>
                progress.Report(BatchProgress.Of(r.success + r.failed, r.total, r.phase, r.success, r.failed)));

        var result = await _generationService.GenerateAsync(genRequest, innerProgress, cancellationToken);

        if (result.BatchId == Guid.Empty)
        {
            _logger.LogError("❌ [Batch] فشل إنشاء الدفعة.");
            throw new InvalidOperationException("فشل إنشاء الدفعة — لم يُرجع BatchId صالح.");
        }

        // تحديث معلومات الدفعة (الاسم، الوصف، المُنشئ)
        await UpdateBatchMetaAsync(result.BatchId, request.Name, request.Description, request.CreatedBy, cancellationToken);

        // تحديث العدادات
        await _batchRepo.UpdateCountersAsync(result.BatchId, cancellationToken);

        _logger.LogInformation("✅ [Batch] تم إنشاء الدفعة {BatchId} بنجاح.", result.BatchId);
        return result.BatchId;
    }

    // ─── المزامنة ────────────────────────────────────────────────────────────────

    public async Task<SyncMetrics> SyncBatchAsync(
        Guid batchId,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔄 [Batch] بدء مزامنة الدفعة {BatchId}...", batchId);

        await SetBatchStatusAsync(batchId, BatchStatus.Syncing, BatchSyncStatus.InProgress, cancellationToken);

        try
        {
            var innerProgress = WrapProgress(progress);
            var metrics = await _syncService.ProcessBatchAsync(batchId, innerProgress, cancellationToken);

            await _batchRepo.UpdateCountersAsync(batchId, cancellationToken);
            await FinalizeSyncStatusAsync(batchId, metrics, cancellationToken);

            _logger.LogInformation("✅ [Batch] انتهت مزامنة الدفعة {BatchId}: {Metrics}", batchId, metrics.ToString());
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [Batch] فشل مزامنة الدفعة {BatchId}", batchId);
            await SetBatchErrorAsync(batchId, ex.Message, BatchSyncStatus.Failed, cancellationToken);
            throw;
        }
    }

    public async Task<SyncMetrics> RetryFailedBatchAsync(
        Guid batchId,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔁 [Batch] إعادة محاولة الكروت الفاشلة للدفعة {BatchId}...", batchId);

        // زيادة RetryCount
        await IncrementRetryCountAsync(batchId, cancellationToken);
        await SetBatchStatusAsync(batchId, BatchStatus.Syncing, BatchSyncStatus.Retrying, cancellationToken);

        try
        {
            var innerProgress = WrapProgress(progress);
            var metrics = await _syncService.RetryBatchAsync(batchId, innerProgress, cancellationToken);

            await _batchRepo.UpdateCountersAsync(batchId, cancellationToken);
            await FinalizeSyncStatusAsync(batchId, metrics, cancellationToken);

            return metrics;
        }
        catch (Exception ex)
        {
            await SetBatchErrorAsync(batchId, ex.Message, BatchSyncStatus.Failed, cancellationToken);
            throw;
        }
    }

    public async Task<SyncMetrics> ResumeBatchAsync(
        Guid batchId,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("▶ [Batch] استكمال مزامنة الدفعة {BatchId}...", batchId);
        await SetBatchStatusAsync(batchId, BatchStatus.Syncing, BatchSyncStatus.InProgress, cancellationToken);

        try
        {
            var innerProgress = WrapProgress(progress);
            var metrics = await _syncService.ResumeBatchAsync(batchId, innerProgress, cancellationToken);

            await _batchRepo.UpdateCountersAsync(batchId, cancellationToken);
            await FinalizeSyncStatusAsync(batchId, metrics, cancellationToken);

            return metrics;
        }
        catch (Exception ex)
        {
            await SetBatchErrorAsync(batchId, ex.Message, BatchSyncStatus.Paused, cancellationToken);
            throw;
        }
    }

    public async Task CancelSyncAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("⏹ [Batch] إلغاء مزامنة الدفعة {BatchId}...", batchId);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is null) return;

        batch.SyncStatus  = BatchSyncStatus.Paused;
        batch.Status      = BatchStatus.PartiallyFailed;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ─── الطباعة / PDF ───────────────────────────────────────────────────────────

    public async Task<BatchPrintResult> PrintBatchAsync(
        Guid batchId,
        PrintSettingsDto? settings = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🖨️ [Batch] توليد PDF للدفعة {BatchId}...", batchId);
        await SetBatchPrintStatusAsync(batchId, BatchStatus.Printing, BatchPrintStatus.Generating, cancellationToken);

        var vouchers = await _batchQueryService.GetBatchVouchersAsync(batchId, cancellationToken);
        if (!vouchers.Any())
            return BatchPrintResult.Failure("لا توجد كروت في الدفعة للطباعة.");

        var printSettings = settings ?? new PrintSettingsDto();
        var pdfResult = await _printService.GeneratePdfAsync(vouchers.ToList(), printSettings, null, cancellationToken);

        if (!pdfResult.IsSuccess)
        {
            await SetBatchPrintStatusAsync(batchId, BatchStatus.PartiallyFailed, BatchPrintStatus.Failed, cancellationToken);
            return BatchPrintResult.Failure(pdfResult.ErrorMessage ?? "فشل توليد PDF");
        }

        // حفظ مسار PDF في الدفعة
        var pdfPath = GetPdfPath(batchId);
        await File.WriteAllBytesAsync(pdfPath, pdfResult.Value, cancellationToken);

        var hash = ComputeHash(pdfResult.Value);
        await SavePdfPathAsync(batchId, pdfPath, hash, cancellationToken);
        await _batchRepo.UpdateCountersAsync(batchId, cancellationToken);

        _logger.LogInformation("✅ [Batch] PDF محفوظ: {Path}", pdfPath);
        return BatchPrintResult.Success(pdfPath, vouchers.Count, hash);
    }

    public async Task<BatchPrintResult> ReprintBatchAsync(
        Guid batchId,
        PrintSettingsDto? settings = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔁 [Batch] إعادة طباعة الدفعة {BatchId}...", batchId);

        // حذف PDF القديم إن وُجد
        var batch = await _batchRepo.GetAsync(batchId, cancellationToken);
        if (batch?.PdfPath is not null && File.Exists(batch.PdfPath))
        {
            try { File.Delete(batch.PdfPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "⚠️ لم يُحذف PDF القديم."); }
        }

        // إعادة التوليد
        return await PrintBatchAsync(batchId, settings, cancellationToken);
    }

    // ─── الإدارة ──────────────────────────────────────────────────────────────────

    public async Task DeleteBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🗑️ [Batch] حذف الدفعة {BatchId} وكروتها...", batchId);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // حذف الكروت cascade
            var vouchers = await db.Vouchers
                .IgnoreQueryFilters()
                .Where(v => v.BatchId == batchId)
                .ToListAsync(cancellationToken);

            foreach (var v in vouchers) { v.IsDeleted = true; v.UpdatedAt = DateTime.UtcNow; }
            await db.SaveChangesAsync(cancellationToken);

            // حذف الدفعة
            var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
            if (batch is not null) { batch.IsDeleted = true; batch.UpdatedAt = DateTime.UtcNow; }
            await db.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("✅ [Batch] تم حذف الدفعة {BatchId} و{Count} كرت.", batchId, vouchers.Count);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ArchiveBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is null) return;

        batch.Status    = BatchStatus.Archived;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("🗃️ [Batch] تم أرشفة الدفعة {BatchId}.", batchId);
    }

    public async Task<bool> OpenPdfFolderAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepo.GetAsync(batchId, cancellationToken);
        if (batch?.PdfPath is null) return false;

        var folder = Path.GetDirectoryName(batch.PdfPath);
        if (folder is null || !Directory.Exists(folder)) return false;

        System.Diagnostics.Process.Start("explorer.exe", folder);
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private async Task UpdateBatchMetaAsync(Guid batchId, string name, string description, string createdBy, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return;

        if (!string.IsNullOrWhiteSpace(name))        batch.Name        = name;
        if (!string.IsNullOrWhiteSpace(description)) batch.Description = description;
        if (!string.IsNullOrWhiteSpace(createdBy))   batch.CreatedBy   = createdBy;
        batch.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task SetBatchStatusAsync(Guid batchId, BatchStatus status, BatchSyncStatus syncStatus, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return;
        batch.Status     = status;
        batch.SyncStatus = syncStatus;
        batch.UpdatedAt  = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task SetBatchPrintStatusAsync(Guid batchId, BatchStatus status, BatchPrintStatus printStatus, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return;
        batch.Status      = status;
        batch.PrintStatus = printStatus;
        batch.UpdatedAt   = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task SetBatchErrorAsync(Guid batchId, string error, BatchSyncStatus syncStatus, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return;
        batch.LastError   = error;
        batch.SyncStatus  = syncStatus;
        batch.Status      = BatchStatus.PartiallyFailed;
        batch.UpdatedAt   = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task IncrementRetryCountAsync(Guid batchId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return;
        batch.RetryCount++;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task FinalizeSyncStatusAsync(Guid batchId, SyncMetrics metrics, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return;

        batch.LastSyncTime = DateTime.UtcNow;
        batch.SyncStatus   = metrics.Failed > 0 ? BatchSyncStatus.PartiallyFailed : BatchSyncStatus.Completed;
        batch.Status       = metrics.Failed > 0 ? BatchStatus.PartiallyFailed     : BatchStatus.Synced;

        if (metrics.Failed == 0 && batch.FailedCards == 0)
            batch.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private async Task SavePdfPathAsync(Guid batchId, string pdfPath, string hash, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return;
        batch.PdfPath      = pdfPath;
        batch.PdfHash      = hash;
        batch.PrintStatus  = BatchPrintStatus.Generated;
        batch.LastPrintTime = DateTime.UtcNow;
        batch.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static IProgress<(int success, int failed, int total)>? WrapProgress(IProgress<BatchProgress>? outer)
    {
        if (outer is null) return null;
        return new Progress<(int success, int failed, int total)>(r =>
            outer.Report(BatchProgress.Of(r.success + r.failed, r.total, "مزامنة", r.success, r.failed)));
    }

    private static string GetPdfPath(Guid batchId)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LuxCard", "PDFs");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"Batch_{batchId:N}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    private static string ComputeHash(byte[] data)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(data);
        return Convert.ToHexString(bytes)[..16];
    }
}
