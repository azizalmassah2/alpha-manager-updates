using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using MikroTikVoucherPrinter.Domain.Entities.Operations;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Operations;

public class OperationHistoryService : IOperationHistoryService
{
    private readonly PlatformDbContext _dbContext;

    public OperationHistoryService(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<OperationAuditRecord>> GetAuditHistoryAsync(int page = 1, int pageSize = 50)
    {
        return await _dbContext.OperationAuditRecords
            .AsNoTracking()
            .OrderByDescending(x => x.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<OperationAuditRecord>> GetDeviceHistoryAsync(Guid routerId, int page = 1, int pageSize = 50)
    {
        return await _dbContext.OperationAuditRecords
            .AsNoTracking()
            .Where(x => x.RouterId == routerId)
            .OrderByDescending(x => x.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<OperationAuditRecord>> GetFailedOperationsAsync(int page = 1, int pageSize = 50)
    {
        return await _dbContext.OperationAuditRecords
            .AsNoTracking()
            .Where(x => x.Status == "Failed")
            .OrderByDescending(x => x.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
