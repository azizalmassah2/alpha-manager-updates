using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Infrastructure.Data;

using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class BatchQueryService : IBatchQueryService
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly IActiveRouterContext _routerContext;

    public BatchQueryService(
        IDbContextFactory<LuxCardDbContext> dbFactory,
        IActiveRouterContext routerContext)
    {
        _dbFactory = dbFactory;
        _routerContext = routerContext;
    }

    public async Task<IReadOnlyList<BatchDto>> GetAllBatchesAsync(CancellationToken cancellationToken = default)
    {
        var routerId = _routerContext.CurrentRouterId;
        if (routerId == null || routerId == Guid.Empty)
        {
            return Array.Empty<BatchDto>();
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batches = await db.Batches
            .AsNoTracking()
            .Where(b => b.RouterId == routerId.Value)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return batches.Select(MapToDto).ToList();
    }

    public async Task<BatchDto?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.Batches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        return batch is null ? null : MapToDto(batch);
    }

    public async Task<IReadOnlyList<VoucherDto>> GetBatchVouchersAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vouchers
            .Include(v => v.Agent)
            .AsNoTracking()
            .Where(v => v.BatchId == batchId)
            .Select(v => new VoucherDto
            {
                Id             = v.Id,
                Username       = v.Username,
                Password       = v.Password,
                Profile        = v.ProfileName,
                Price          = v.Price,
                Status         = v.Status,
                SyncStatus     = v.SyncStatus,
                PrintStatus    = v.PrintStatus,
                CreatedAt      = v.CreatedAt,
                BatchId        = v.BatchId,
                CredentialMode = v.CredentialMode,
                DataOrigin     = VoucherDataOrigin.Local,
                AgentName      = v.Agent != null ? v.Agent.Name : "-",
                IsFavorite     = v.IsFavorite
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BatchDto>> GetBatchesWithFailedSyncAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batches = await db.Batches
            .AsNoTracking()
            .Where(b => b.FailedCards > 0 &&
                        b.Status != BatchStatus.Archived &&
                        b.Status != BatchStatus.Cancelled)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return batches.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<BatchDto>> GetActiveBatchesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var batches = await db.Batches
            .AsNoTracking()
            .Where(b => b.Status == BatchStatus.Generating ||
                        b.Status == BatchStatus.Syncing     ||
                        b.Status == BatchStatus.Printing)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return batches.Select(MapToDto).ToList();
    }

    private static BatchDto MapToDto(Domain.Entities.Batch b) => new()
    {
        Id            = b.Id,
        Name          = b.Name,
        Description   = b.Description,
        ProfileName   = b.ProfileName,
        CreatedBy     = b.CreatedBy,
        CreatedAt     = b.CreatedAt,
        TotalCards    = b.TotalCards,
        GeneratedCards = b.GeneratedCards,
        SyncedCards   = b.SyncedCards,
        FailedCards   = b.FailedCards,
        PrintedCards  = b.PrintedCards,
        Status        = b.Status,
        SyncStatus    = b.SyncStatus,
        PrintStatus   = b.PrintStatus,
        PdfPath       = b.PdfPath,
        PdfHash       = b.PdfHash,
        LastError     = b.LastError,
        RetryCount    = b.RetryCount,
        StartedAt     = b.StartedAt,
        CompletedAt   = b.CompletedAt,
        CancelledAt   = b.CancelledAt,
        LastSyncTime  = b.LastSyncTime,
        LastPrintTime = b.LastPrintTime,
    };
}
