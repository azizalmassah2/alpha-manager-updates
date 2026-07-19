using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface ISyncService
{
    Task<SyncMetrics> ProcessPendingAsync(CancellationToken cancellationToken = default);
    Task<SyncMetrics> ProcessPendingAsync(IProgress<(int success, int failed, int total)>? progress, CancellationToken cancellationToken = default);
    Task<SyncMetrics> ProcessBatchAsync(Guid batchId, IProgress<(int success, int failed, int total)>? progress, CancellationToken cancellationToken = default);
    Task<SyncMetrics> RetryFailedAsync(CancellationToken cancellationToken = default);
}
