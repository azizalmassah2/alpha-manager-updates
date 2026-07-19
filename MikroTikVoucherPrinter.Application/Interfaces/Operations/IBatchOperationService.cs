using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.Interfaces.Operations;

public interface IBatchOperationService
{
    Task<Guid> ExecuteModemBatchRebootAsync(IEnumerable<Guid> modemIds);
    Task<Guid> ExecuteWirelessSignalCheckAsync(IEnumerable<Guid> wirelessDeviceIds);
    Task<Guid> ExecuteRouterBackupAsync();
}
