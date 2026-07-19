using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IVoucherRestoreService
{
    Task<List<VoucherRestoreResult>> RestoreVouchersAsync(IEnumerable<Guid> voucherIds, CancellationToken cancellationToken = default);
}
