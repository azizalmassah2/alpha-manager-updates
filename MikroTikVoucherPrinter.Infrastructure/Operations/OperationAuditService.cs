using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using MikroTikVoucherPrinter.Domain.Entities.Operations;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Operations;

public class OperationAuditService : IOperationAuditService
{
    private readonly PlatformDbContext _dbContext;

    public OperationAuditService(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordStartAsync(Guid jobId, Guid? routerId, string userId)
    {
        var record = new OperationAuditRecord
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            RouterId = routerId,
            UserId = userId,
            StartTime = DateTime.UtcNow,
            Status = "Running",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.OperationAuditRecords.Add(record);
        await _dbContext.SaveChangesAsync();
    }

    public async Task RecordCompletionAsync(Guid jobId, Guid? routerId, string status, string? failureReason = null)
    {
        var record = await _dbContext.OperationAuditRecords
            .FirstOrDefaultAsync(r => r.JobId == jobId && r.RouterId == routerId && r.EndTime == null);

        if (record != null)
        {
            record.EndTime = DateTime.UtcNow;
            record.Status = status;
            record.FailureReason = failureReason;
            record.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }
}
