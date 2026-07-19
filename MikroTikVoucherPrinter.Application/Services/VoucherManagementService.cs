using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Entities;

namespace MikroTikVoucherPrinter.Application.Services;

public class VoucherManagementService : IVoucherManagementService
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly IMikroTikIntegrationService _mikroTikService;
    private readonly IVoucherRestoreService _restoreService;
    private readonly ILogger<VoucherManagementService> _logger;

    public VoucherManagementService(
        IVoucherRepository voucherRepository,
        IMikroTikIntegrationService mikroTikService,
        IVoucherRestoreService restoreService,
        ILogger<VoucherManagementService> logger)
    {
        _voucherRepository = voucherRepository;
        _mikroTikService = mikroTikService;
        _restoreService = restoreService;
        _logger = logger;
    }

    public async Task<(int deleted, int failed)> SoftDeleteVouchersAsync(IEnumerable<Guid> voucherIds, CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Debug.WriteLine($"[DELETE-04] Delete Command Started | Time: {DateTime.Now:HH:mm:ss.fff}");
        int deleted = 0;
        int failed = 0;

        var ids = voucherIds.ToList();
        if (ids.Count == 0) return (deleted, failed);

        try
        {
            // 1. جلب كل الكيانات من قاعدة البيانات
            var entities = new List<MikroTikVoucherPrinter.Domain.Entities.Voucher>();
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entity = await _voucherRepository.GetAsync(id, cancellationToken);
                if (entity == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DELETE-WARN] Voucher {id} not found in local DB. | Time: {DateTime.Now:HH:mm:ss.fff}");
                    _logger.LogWarning("⚠️ [SoftDelete] Voucher {Id} not found in DB.", id);
                    failed++;
                }
                else
                {
                    entities.Add(entity);
                }
            }

            if (entities.Count == 0) return (deleted, failed);

            // 2. حذف جماعي من الراوتر (اتصال واحد)
            var usersToDelete = entities.Select(e => (e.Username, e.MikroTikUserId)).ToList();
            var routerResults = await _mikroTikService.DeleteUsersBulkAsync(usersToDelete, cancellationToken: cancellationToken);

            System.Diagnostics.Debug.WriteLine($"[DELETE-07] Database Update Started | Time: {DateTime.Now:HH:mm:ss.fff}");
            // 3. تحديث قاعدة البيانات للكروت التي نجح حذفها من الراوتر
            foreach (var entity in entities)
            {
                if (routerResults.TryGetValue(entity.Username, out var result) && result.IsSuccess)
                {
                    entity.IsDeleted = true;
                    entity.DeletedDate = DateTime.UtcNow;
                    entity.DeletedSource = VoucherDeletedSource.LocalConsole;
                    entity.MarkAsPendingForDeleteOrRestore();

                    await _voucherRepository.UpdateAsync(entity, cancellationToken);
                    _logger.LogInformation("🗑️ [SoftDelete] Voucher '{Username}' deleted from router and marked locally.", entity.Username);
                    deleted++;
                }
                else
                {
                    var errorMsg = routerResults.TryGetValue(entity.Username, out var failResult)
                        ? failResult.ErrorMessage
                        : "No result returned from router.";

                    System.Diagnostics.Debug.WriteLine($"[DELETE-FAIL-DETAIL] Voucher delete failed: Username={entity.Username}, RouterId={entity.RouterId}, Error={errorMsg} | Time: {DateTime.Now:HH:mm:ss.fff}");
                    _logger.LogError(
                        "❌ [SoftDelete] Failed to delete '{Username}' from router: {Error}. Local record NOT modified.",
                        entity.Username, errorMsg);
                    failed++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[DELETE-08] Database Update Finished | Time: {DateTime.Now:HH:mm:ss.fff}");
            return (deleted, failed);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DELETE-ERROR] Exception in SoftDeleteVouchersAsync: {ex} | Time: {DateTime.Now:HH:mm:ss.fff}");
            throw; // إعادة رمي الاستثناء للتتبع
        }
    }


    public async Task<(int deleted, int failed)> PermanentDeleteVouchersAsync(IEnumerable<Guid> voucherIds, CancellationToken cancellationToken = default)
    {
        int deleted = 0;
        int failed = 0;

        foreach (var id in voucherIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entity = await _voucherRepository.GetAsync(id, cancellationToken);
                if (entity != null)
                {
                    await _voucherRepository.HardDeleteAsync(entity, cancellationToken);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed to permanently delete voucher {Id}", id);
            }
        }

        return (deleted, failed);
    }

    public async Task<List<VoucherRestoreResult>> RestoreVouchersAsync(IEnumerable<Guid> voucherIds, CancellationToken cancellationToken = default)
    {
        return await _restoreService.RestoreVouchersAsync(voucherIds, cancellationToken);
    }
}
