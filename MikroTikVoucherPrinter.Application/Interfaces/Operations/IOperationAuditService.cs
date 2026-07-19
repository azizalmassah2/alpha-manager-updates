using System;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces.Operations;

public interface IOperationAuditService
{
    Task RecordStartAsync(Guid jobId, Guid? deviceId, string userId);
    Task RecordCompletionAsync(Guid jobId, Guid? deviceId, string status, string? failureReason = null);
}
