using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface ISyncService
{
    Task<SyncMetrics> ProcessPendingAsync(CancellationToken cancellationToken = default);
    Task<SyncMetrics> ProcessPendingAsync(IProgress<(int success, int failed, int total)>? progress, CancellationToken cancellationToken = default);
    Task<SyncMetrics> ProcessBatchAsync(Guid batchId, IProgress<(int success, int failed, int total)>? progress, CancellationToken cancellationToken = default);
    Task<SyncMetrics> RetryFailedAsync(CancellationToken cancellationToken = default);

    // ─── Batch-level (جديد) ────────────────────────────────────────────────────

    /// <summary>
    /// يعيد محاولة الكروت الفاشلة لهذه الدفعة فقط (بدون المساس ببقية النظام).
    /// </summary>
    Task<SyncMetrics> RetryBatchAsync(
        Guid batchId,
        IProgress<(int success, int failed, int total)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// يستكمل المزامنة من آخر نقطة توقف — يعالج Pending فقط لهذه الدفعة.
    /// </summary>
    Task<SyncMetrics> ResumeBatchAsync(
        Guid batchId,
        IProgress<(int success, int failed, int total)>? progress = null,
        CancellationToken cancellationToken = default);
}
