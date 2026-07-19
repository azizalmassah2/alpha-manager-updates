using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using MikroTikVoucherPrinter.Domain.Entities.Operations;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Operations;

public class OperationEngine : IOperationEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<OperationJob> _jobChannel;

    public OperationEngine(IServiceScopeFactory scopeFactory, Channel<OperationJob> jobChannel)
    {
        _scopeFactory = scopeFactory;
        _jobChannel = jobChannel;
    }

    public async Task<Guid> QueueOperationAsync(OperationType type, DeviceRole targetRole, IEnumerable<Guid> targetRouterIds, string name = "")
    {
        var job = new OperationJob
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? $"{type} Job" : name,
            OperationType = type,
            TargetRole = targetRole,
            TargetRouterIds = JsonSerializer.Serialize(targetRouterIds),
            State = OperationState.Queued,
            CreatedAt = DateTime.UtcNow
        };

        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.OperationJobs.Add(job);
            await dbContext.SaveChangesAsync();
        }

        // Pass to background worker
        await _jobChannel.Writer.WriteAsync(job);

        return job.Id;
    }

    public async Task CancelOperationAsync(Guid jobId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        
        var job = await dbContext.OperationJobs.FindAsync(jobId);
        if (job != null && job.State == OperationState.Queued || job?.State == OperationState.Running)
        {
            job.State = OperationState.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            job.ResultMessage = "Operation was cancelled by user.";
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<OperationJob?> GetOperationStatusAsync(Guid jobId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await dbContext.OperationJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId);
    }

    public async Task<IEnumerable<OperationJob>> GetRunningOperationsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await dbContext.OperationJobs
            .AsNoTracking()
            .Where(j => j.State == OperationState.Running || j.State == OperationState.Queued)
            .ToListAsync();
    }
}
