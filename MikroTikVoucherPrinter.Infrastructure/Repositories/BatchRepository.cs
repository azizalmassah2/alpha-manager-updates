using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Repositories;

public class BatchRepository : GenericRepository<Batch>, IBatchRepository
{
    public BatchRepository(IDbContextFactory<LuxCardDbContext> dbFactory) : base(dbFactory)
    {
    }

    public async Task<IReadOnlyList<Batch>> GetAllBatchesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Batches
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Batch?> GetBatchWithVouchersAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Batches
            .Include(b => b.Vouchers)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
    }

    public async Task<int> GetPendingVoucherCountAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vouchers
            .IgnoreQueryFilters()
            .CountAsync(v => v.BatchId == batchId &&
                             v.SyncStatus == SyncStatus.Pending &&
                             !v.IsDeleted, cancellationToken);
    }

    public async Task<int> GetFailedVoucherCountAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vouchers
            .IgnoreQueryFilters()
            .CountAsync(v => v.BatchId == batchId &&
                             v.SyncStatus == SyncStatus.Failed &&
                             !v.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// يُعيد حساب كل عدادات الدفعة من قاعدة البيانات ويحفظها.
    /// يجب استدعاؤه بعد كل Sync أو Print أو Delete.
    /// </summary>
    public async Task UpdateCountersAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);

        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is null) return;

        var counters = await db.Vouchers
            .IgnoreQueryFilters()
            .Where(v => v.BatchId == batchId && !v.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total   = g.Count(),
                Synced  = g.Count(v => v.SyncStatus == SyncStatus.Synced),
                Failed  = g.Count(v => v.SyncStatus == SyncStatus.Failed),
                Printed = g.Count(v => v.PrintStatus == VoucherPrintStatus.Printed ||
                                       v.PrintStatus == VoucherPrintStatus.PdfGenerated)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (counters is not null)
        {
            batch.GeneratedCards = counters.Total;
            batch.SyncedCards    = counters.Synced;
            batch.FailedCards    = counters.Failed;
            batch.PrintedCards   = counters.Printed;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Batch>> GetBatchesWithFailedVouchersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Batches
            .AsNoTracking()
            .Where(b => b.FailedCards > 0 &&
                        b.Status != BatchStatus.Archived &&
                        b.Status != BatchStatus.Cancelled)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Batch>> GetActiveBatchesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Batches
            .AsNoTracking()
            .Where(b => b.Status == BatchStatus.Generating ||
                        b.Status == BatchStatus.Syncing     ||
                        b.Status == BatchStatus.Printing)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
