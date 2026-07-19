using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class VoucherRestoreService : IVoucherRestoreService
{
    private readonly IMikroTikIntegrationService _mikroTikService;
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly IDbContextFactory<PlatformDbContext> _platformDbFactory;
    private readonly ILogger<VoucherRestoreService> _logger;

    public VoucherRestoreService(
        IMikroTikIntegrationService mikroTikService,
        IDbContextFactory<LuxCardDbContext> dbFactory,
        IDbContextFactory<PlatformDbContext> platformDbFactory,
        ILogger<VoucherRestoreService> logger)
    {
        _mikroTikService = mikroTikService;
        _dbFactory = dbFactory;
        _platformDbFactory = platformDbFactory;
        _logger = logger;
    }

    public async Task<List<VoucherRestoreResult>> RestoreVouchersAsync(IEnumerable<Guid> voucherIds, CancellationToken cancellationToken = default)
    {
        var results = new List<VoucherRestoreResult>();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var id in voucherIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var voucher = await db.Vouchers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

                if (voucher == null)
                {
                    sw.Stop();
                    results.Add(VoucherRestoreResult.Failed(id, string.Empty, RestoreStatus.ValidationFailed, "Voucher not found locally.", sw.ElapsedMilliseconds));
                    continue;
                }

                if (!voucher.IsDeleted)
                {
                    sw.Stop();
                    results.Add(VoucherRestoreResult.Failed(id, voucher.Username, RestoreStatus.ValidationFailed, "Voucher is not deleted.", sw.ElapsedMilliseconds));
                    continue;
                }

                var routerExists = await platformDb.Routers.AnyAsync(r => r.Id == voucher.RouterId, cancellationToken);
                if (!routerExists)
                {
                    sw.Stop();
                    results.Add(VoucherRestoreResult.Failed(id, voucher.Username, RestoreStatus.ValidationFailed, $"Router configuration {voucher.RouterId} does not exist.", sw.ElapsedMilliseconds));
                    continue;
                }

                var profileExists = await db.Profiles.AnyAsync(p => p.RouterId == voucher.RouterId && p.Name == voucher.ProfileName, cancellationToken);
                if (!profileExists)
                {
                    sw.Stop();
                    results.Add(VoucherRestoreResult.Failed(id, voucher.Username, RestoreStatus.ValidationFailed, $"Profile '{voucher.ProfileName}' does not exist on this router.", sw.ElapsedMilliseconds));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(voucher.Username) || string.IsNullOrWhiteSpace(voucher.ProfileName))
                {
                    sw.Stop();
                    results.Add(VoucherRestoreResult.Failed(id, voucher.Username, RestoreStatus.ValidationFailed, "Required fields Username or ProfileName are empty.", sw.ElapsedMilliseconds));
                    continue;
                }

                _logger.LogInformation("RestoreAttempt: Voucher {VoucherId} (Username: {Username}) on Router {RouterId}", id, voucher.Username, voucher.RouterId);

                var createResult = await _mikroTikService.CreateUserAsync(voucher.Username, voucher.EffectivePassword, voucher.ProfileName, cancellationToken);

                sw.Stop();
                long duration = sw.ElapsedMilliseconds;

                if (createResult.IsSuccess)
                {
                    var mtResult = createResult.Value;
                    if (mtResult.WasAlreadyPresent)
                    {
                        bool profileMatch = string.Equals(mtResult.ProfileName, voucher.ProfileName, StringComparison.OrdinalIgnoreCase);
                        bool disabledMatch = mtResult.IsDisabled == voucher.IsDisabled;

                        if (profileMatch && disabledMatch)
                        {
                            voucher.IsDeleted = false;
                            voucher.DeletedDate = null;
                            voucher.DeletedSource = null;
                            voucher.MarkAsSynced(mtResult.Id);
                            db.Entry(voucher).State = EntityState.Modified;

                            results.Add(VoucherRestoreResult.Reconciled(id, voucher.Username, duration));
                            _logger.LogInformation("RestoreReconciled: Voucher {VoucherId} (Username: {Username}) reconciled with existing MikroTik user ID {MtId}. Duration: {DurationMs}ms", id, voucher.Username, mtResult.Id, duration);
                        }
                        else
                        {
                            var conflictReason = $"Username already exists on router with conflicting properties. Router: [Profile: {mtResult.ProfileName}, Disabled: {mtResult.IsDisabled}], Local: [Profile: {voucher.ProfileName}, Disabled: {voucher.IsDisabled}]";
                            results.Add(VoucherRestoreResult.Conflict(id, voucher.Username, conflictReason, duration));
                            _logger.LogWarning("RestoreConflict: Voucher {VoucherId} (Username: {Username}). Reason: {Reason}. Duration: {DurationMs}ms", id, voucher.Username, conflictReason, duration);
                            
                            await transaction.RollbackAsync(cancellationToken);
                            
                            foreach (var res in results)
                            {
                                if (res.Status == RestoreStatus.Success || res.Status == RestoreStatus.AlreadyExistsReconciled)
                                {
                                    res.Status = RestoreStatus.UnexpectedError;
                                    res.ErrorMessage = "Aborted due to conflict on another voucher in the restore batch.";
                                }
                            }
                            return results;
                        }
                    }
                    else
                    {
                        voucher.IsDeleted = false;
                        voucher.DeletedDate = null;
                        voucher.DeletedSource = null;
                        voucher.MarkAsSynced(mtResult.Id);
                        db.Entry(voucher).State = EntityState.Modified;

                        results.Add(VoucherRestoreResult.Succeeded(id, voucher.Username, duration));
                        _logger.LogInformation("RestoreSucceeded: Voucher {VoucherId} (Username: {Username}) created successfully. Duration: {DurationMs}ms", id, voucher.Username, duration);
                    }
                }
                else
                {
                    var errorMsg = createResult.ErrorMessage ?? "Unknown router error";
                    results.Add(VoucherRestoreResult.Failed(id, voucher.Username, RestoreStatus.RouterError, errorMsg, duration));
                    _logger.LogError("RestoreFailed: Voucher {VoucherId} (Username: {Username}). Router Error: {Error}. Duration: {DurationMs}ms", id, voucher.Username, errorMsg, duration);

                    await transaction.RollbackAsync(cancellationToken);
                    
                    foreach (var res in results)
                    {
                        if (res.Status == RestoreStatus.Success || res.Status == RestoreStatus.AlreadyExistsReconciled)
                        {
                            res.Status = RestoreStatus.UnexpectedError;
                            res.ErrorMessage = "Aborted due to router failure on another voucher in the restore batch.";
                        }
                    }
                    return results;
                }
            }

            bool hasFailures = results.Any(r => r.Status != RestoreStatus.Success && r.Status != RestoreStatus.AlreadyExistsReconciled);
            if (hasFailures)
            {
                await transaction.RollbackAsync(cancellationToken);
                
                foreach (var res in results)
                {
                    if (res.Status == RestoreStatus.Success || res.Status == RestoreStatus.AlreadyExistsReconciled)
                    {
                        res.Status = RestoreStatus.UnexpectedError;
                        res.ErrorMessage = "Aborted due to validation/other failure on one of the vouchers in the batch.";
                    }
                }
            }
            else
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in RestoreVouchersAsync. Rolling back database transaction.");
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch { }

            results.Add(VoucherRestoreResult.Failed(Guid.Empty, string.Empty, RestoreStatus.UnexpectedError, ex.Message, 0));
            
            foreach (var res in results)
            {
                if (res.Status == RestoreStatus.Success || res.Status == RestoreStatus.AlreadyExistsReconciled)
                {
                    res.Status = RestoreStatus.UnexpectedError;
                    res.ErrorMessage = "Aborted due to unexpected transaction exception: " + ex.Message;
                }
            }
        }

        return results;
    }
}
