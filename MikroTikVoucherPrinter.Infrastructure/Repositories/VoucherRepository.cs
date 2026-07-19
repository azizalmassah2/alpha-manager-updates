using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace MikroTikVoucherPrinter.Infrastructure.Repositories;

public class VoucherRepository : GenericRepository<Voucher>, IVoucherRepository
{
    public VoucherRepository(IDbContextFactory<LuxCardDbContext> dbFactory) : base(dbFactory)
    {
    }

    public async Task<Voucher?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Vouchers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Username == username, cancellationToken);
    }

    public async Task<IReadOnlyList<Voucher>> GetPendingSyncAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        var activeRouterId = db.CurrentRouterId ?? Guid.Empty;
        return await db.Vouchers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => v.SyncStatus == SyncStatus.Pending && v.RouterId == activeRouterId)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Voucher>> GetFailedSyncAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        var activeRouterId = db.CurrentRouterId ?? Guid.Empty;
        return await db.Vouchers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => v.SyncStatus == SyncStatus.Failed && v.RouterId == activeRouterId)
            .OrderBy(v => v.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<BulkInsertResult> BulkInsertSafelyAsync(IEnumerable<Voucher> vouchers, CancellationToken cancellationToken = default)
    {
        var result = new BulkInsertResult();
        var uniqueVouchers = new List<Voucher>();
        var seenUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in vouchers)
        {
            if (seenUsernames.Add(v.Username))
                uniqueVouchers.Add(v);
            else
            {
                result.FailedCount++;
                result.FailedUsernames.Add(v.Username);
            }
        }

        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        var set = db.Vouchers;

        var usernamesToCheck = uniqueVouchers.Select(u => u.Username).ToList();
        var existingInDb = await set
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => usernamesToCheck.Contains(v.Username))
            .Select(v => v.Username)
            .ToListAsync(cancellationToken);

        var finalInsertList = new List<Voucher>();
        foreach (var v in uniqueVouchers)
        {
            if (existingInDb.Contains(v.Username))
            {
                result.FailedCount++;
                result.FailedUsernames.Add(v.Username);
            }
            else
                finalInsertList.Add(v);
        }

        if (!finalInsertList.Any()) return result;

        int maxRetries = 5;
        int delayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await set.AddRangeAsync(finalInsertList, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                result.SuccessCount = finalInsertList.Count;
                return result;
            }
            catch (DbUpdateException ex) when (IsSqliteLocked(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();

                if (i == maxRetries - 1) throw;
                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                db.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        return result;
    }

    private static bool IsSqliteLocked(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 5)
                return true;
            current = current.InnerException;
        }
        return false;
    }
}
