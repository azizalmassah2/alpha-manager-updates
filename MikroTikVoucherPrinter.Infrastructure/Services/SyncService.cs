// using System.Collections.Concurrent; â€” ط£ظڈط²ظٹظ„ ظ…ط¹ _userLocks (Dead Code cleanup)
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class SyncService : ISyncService
{
    private readonly IVoucherRepository _voucherRepo;
    private readonly IGenericRepository<Voucher> _genericVoucherRepo;
    private readonly IMikroTikIntegrationService _mikroTikIntegrationService;
    private readonly ILogger<SyncService> _logger;

    // ظ…ظ„ط§ط­ط¸ط©: _userLocks ظˆ _throttle ط£ظڈط²ظٹظ„ط§ â€” ظ„ظ… ظٹظڈط³طھط®ط¯ظ…ط§ ظپظٹ ط£ظٹ ظ…ط³ط§ط± طھظ†ظپظٹط°.
    // Circuit Breaker ظپظٹ MikroTikIntegrationService ظٹط¤ط¯ظٹ ظ†ظپط³ ط¯ظˆط± ط§ظ„ط­ظ…ط§ظٹط©.

    public SyncService(
        IVoucherRepository voucherRepo,
        IGenericRepository<Voucher> genericVoucherRepo,
        IMikroTikIntegrationService mikroTikIntegrationService,
        ILogger<SyncService> logger)
    {
        _voucherRepo = voucherRepo;
        _genericVoucherRepo = genericVoucherRepo;
        _mikroTikIntegrationService = mikroTikIntegrationService;
        _logger = logger;
    }

    public Task<SyncMetrics> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        return ProcessPendingAsync(null, cancellationToken);
    }

    public async Task<SyncMetrics> ProcessPendingAsync(IProgress<(int success, int failed, int total)>? progress, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ًں”„ [Sync Engine] ط¬ط§ط±ظٹ ط§ظ„ط¨ط­ط« ط¹ظ† ظƒط±ظˆطھ ظ…ط¹ظ„ظ‚ط© (Pending)...");
        var pendingVouchers = await _voucherRepo.GetPendingSyncAsync(cancellationToken);
        return await ProcessVouchersListAsync(pendingVouchers, progress, cancellationToken);
    }

    public async Task<SyncMetrics> ProcessBatchAsync(Guid batchId, IProgress<(int success, int failed, int total)>? progress, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ًں”„ [Sync Engine] ط¬ط§ط±ظٹ ط§ظ„ط¨ط­ط« ط¹ظ† ط§ظ„ظƒط±ظˆطھ ظ„ظ„ط¯ظپط¹ط© {BatchId}...", batchId);
        // ظ†ط³طھط®ط¯ظ… ظ…ط³طھظˆط¯ط¹ ط§ظ„ظƒط±ظˆطھ ظ„ط¬ظ„ط¨ ط§ظ„ط¯ظپط¹ط©طŒ ظˆظ„ظƒظ† ط³ظ†طµظپظٹ ط§ظ„ظƒط±ظˆطھ ط§ظ„ظ…ط¹ظ„ظ‚ط© ظپظ‚ط· (ظ‚ظٹط¯ ط§ظ„ط§ظ†طھط¸ط§ط±)
        var allPending = await _voucherRepo.GetPendingSyncAsync(cancellationToken);
        var batchVouchers = allPending.Where(v => v.BatchId == batchId).ToList();
        return await ProcessVouchersListAsync(batchVouchers, progress, cancellationToken);
    }

    private async Task<SyncMetrics> ProcessVouchersListAsync(IReadOnlyCollection<Voucher> vouchersToProcess, IProgress<(int success, int failed, int total)>? progress, CancellationToken cancellationToken)
    {
        var metrics = new SyncMetrics();

        if (vouchersToProcess.Count == 0)
        {
            _logger.LogInformation("✅ [Sync Engine] ط§ظ„ط¨ط­ط« ط¹ظ† ظƒط±ظˆطھ ظ…ط¹ظ„ظ‚ط© ظ„ظ…ط¹ط§ظ„ط¬طھظ‡ط§.");
            return metrics;
        }

        _logger.LogInformation("🚀 [Sync Engine] طھظ… ط§ظ„ط¹ط«ظˆط± ط¹ظ„ظ‰ {Count} ظƒط±طھ. ط¨ط¯ط، ط§ظ„ظ…ط¹ط§ظ„ط¬ط©...", vouchersToProcess.Count);

        var creations = vouchersToProcess.Where(v => !v.IsDeleted).ToList();
        var deletions = vouchersToProcess.Where(v => v.IsDeleted).ToList();

        // 1. Process Deletions
        foreach (var voucher in deletions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var delResult = await _mikroTikIntegrationService.DeleteUserAsync(voucher.Username, voucher.MikroTikUserId, cancellationToken);
                if (delResult.IsSuccess)
                {
                    voucher.MarkAsSyncedForDelete();
                    metrics.IncrementSuccess();
                }
                else
                {
                    voucher.MarkAsFailed($"[{delResult.ErrorType}] {delResult.ErrorMessage}");
                    metrics.IncrementFailed();
                }
            }
            catch (Exception ex)
            {
                voucher.MarkAsFailed($"[{ErrorType.Unexpected}] {ex.Message}");
                metrics.IncrementFailed();
            }
        }

        // 2. Process Creations
        var validCreations = new List<Voucher>();
        foreach (var voucher in creations)
        {
            if (string.IsNullOrEmpty(voucher.ProfileName))
            {
                voucher.MarkAsFailed($"[{ErrorType.Validation}] ط§ظ„ط¨ط§ظ‚ط© ط§ظ„ظ…ط±طھط¨ط·ط© ط¨ظ‡ط°ط§ ط§ظ„ظƒط±طھ ط؛ظٹط± طµط§ظ„ط­ط©.");
                metrics.IncrementFailed();
            }
            else
            {
                validCreations.Add(voucher);
            }
        }

        if (validCreations.Any())
        {
            var usersToSync = validCreations.Select(v => (v.Username, v.EffectivePassword, v.ProfileName)).ToList();
            var results = await _mikroTikIntegrationService.CreateUsersBulkAsync(usersToSync, progress, metrics.Success, metrics.Failed, cancellationToken);

            foreach (var voucher in validCreations)
            {
                if (results.TryGetValue(voucher.Username, out var result))
                {
                    if (result.IsSuccess)
                    {
                        voucher.MarkAsSynced(result.Value.Id);
                        metrics.IncrementSuccess();
                    }
                    else
                    {
                        voucher.MarkAsFailed($"[{result.ErrorType}] {result.ErrorMessage}");
                        metrics.IncrementFailed();
                    }
                }
                else
                {
                    voucher.MarkAsFailed($"[{ErrorType.Unexpected}] ظ„ظ… ظٹطھظ… ط§ظ„ط¹ط«ظˆط± ط¹ظ„ظ‰ ظ†طھظٹط¬ط© ط§ظ„ظ…ط²ط§ظ…ظ†ط©.");
                    metrics.IncrementFailed();
                }
            }
        }

        _logger.LogInformation("ًں’¾ [Sync Engine] ط¬ط§ط±ظٹ ط­ظپط¸ طھط؛ظٹظٹط±ط§طھ {Count} ظƒط±طھ...", vouchersToProcess.Count);
        
        foreach (var voucher in vouchersToProcess)
        {
            try
            {
                await _genericVoucherRepo.UpdateAsync(voucher, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "âڑ ï¸ڈ ظپط´ظ„ ط­ظپط¸ ط­ط§ظ„ط© ط§ظ„ظƒط±طھ {Username}", voucher.Username);
            }
        }

        _logger.LogInformation("ًںژ¯ [Sync Engine] ط§ظ†طھظ‡طھ ط§ظ„ط¹ظ…ظ„ظٹط©. {Metrics}", metrics.ToString());
        return metrics;
    }

    public async Task<SyncMetrics> RetryFailedAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new SyncMetrics();
        _logger.LogInformation("âڈ³ [Sync Engine] ط§ظ„ط¨ط­ط« ط¹ظ† ط§ظ„ظƒط±ظˆطھ ط§ظ„ظپط§ط´ظ„ط© ظ„ظ…ط¹ط§ظ„ط¬طھظ‡ط§...");
        var failedVouchers = await _voucherRepo.GetFailedSyncAsync(cancellationToken);

        // ظپظ„طھط±ط© ط§ظ„ط°ظƒط§ط،: ط§ط³طھط¨ط¹ط§ط¯ ط§ظ„ط£ط®ط·ط§ط، ط§ظ„ظ…ظ†ط·ظ‚ظٹط© ظˆط¥ط¨ظ‚ط§ط، ط£ط®ط·ط§ط، ط§ظ„ط´ط¨ظƒط© ظˆط§ظ„ظ€ External Services ظپظ‚ط·
        var toRetry = failedVouchers
            .Where(v => v.SyncError != null && v.SyncError.Contains($"[{ErrorType.ExternalService}]"))
            .Take(100) // ط­ط¯ ط£ظ‚طµظ‰ ظ„ظ„ط¯ظپط¹ط©
            .ToList();

        int skipped = failedVouchers.Count - toRetry.Count;
        for (int i = 0; i < skipped; i++) metrics.IncrementSkipped();

        if (!toRetry.Any())
        {
            _logger.LogInformation("âœ… [Sync Engine] ظ„ط§ طھظˆط¬ط¯ ظƒط±ظˆطھ ظپط§ط´ظ„ط© طµط§ظ„ط­ط© ظ„ظ„ط¬ط¯ظˆظ„ط© ظ…ظ† ط¬ط¯ظٹط¯.");
            return metrics;
        }

        _logger.LogInformation("ًں”پ [Retry Engine] طھظ… طھط­ط¯ظٹط¯ {Count} ظƒط±طھ ظ„ط¥ط¹ط§ط¯ط© ط¥ط±ط³ط§ظ„ظ‡ط§.", toRetry.Count);

        foreach (var voucher in toRetry)
        {
            voucher.MarkAsPending();
            metrics.IncrementRetries();
            await _genericVoucherRepo.UpdateAsync(voucher, cancellationToken);
        }

        // ط¥طµظ„ط§ط­: ظƒط§ظ†طھ additionalMetrics طھظڈط­ط³ط¨ ط«ظ… طھظڈطھط¬ط§ظ‡ظ„ â€” ط§ظ„ط¢ظ† ظٹطھظ… ط¯ظ…ط¬ظ‡ط§ ظ…ط¹ metrics ط§ظ„ط£طµظ„ظٹط©
        var additionalMetrics = await ProcessPendingAsync(cancellationToken);
        return metrics.Merge(additionalMetrics);
    }
}
