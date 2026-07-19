using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IVoucherManagementService
{
    /// <summary>
    /// Soft deletes the given vouchers and returns the number of successfully deleted vouchers.
    /// </summary>
    Task<(int deleted, int failed)> SoftDeleteVouchersAsync(IEnumerable<Guid> voucherIds, CancellationToken cancellationToken = default);
    Task<(int deleted, int failed)> PermanentDeleteVouchersAsync(IEnumerable<Guid> voucherIds, CancellationToken cancellationToken = default);
    Task<List<VoucherRestoreResult>> RestoreVouchersAsync(IEnumerable<Guid> voucherIds, CancellationToken cancellationToken = default);
}
