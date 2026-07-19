using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities.Operations;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.Interfaces.Operations;

public interface IOperationEngine
{
    Task<Guid> QueueOperationAsync(OperationType type, DeviceRole targetRole, IEnumerable<Guid> targetDeviceIds, string name = "");
    Task CancelOperationAsync(Guid jobId);
    Task<OperationJob?> GetOperationStatusAsync(Guid jobId);
    Task<IEnumerable<OperationJob>> GetRunningOperationsAsync();
}
