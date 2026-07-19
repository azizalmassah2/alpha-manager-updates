using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities.Operations;

namespace MikroTikVoucherPrinter.Application.Interfaces.Operations;

public interface IOperationHistoryService
{
    Task<IEnumerable<OperationAuditRecord>> GetAuditHistoryAsync(int page = 1, int pageSize = 50);
    Task<IEnumerable<OperationAuditRecord>> GetDeviceHistoryAsync(Guid deviceId, int page = 1, int pageSize = 50);
    Task<IEnumerable<OperationAuditRecord>> GetFailedOperationsAsync(int page = 1, int pageSize = 50);
}
