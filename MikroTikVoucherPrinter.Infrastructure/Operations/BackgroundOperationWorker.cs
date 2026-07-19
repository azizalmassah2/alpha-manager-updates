using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using MikroTikVoucherPrinter.Domain.Entities.Operations;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Operations;

public class BackgroundOperationWorker : BackgroundService, IOperationWorker
{
    private readonly Channel<OperationJob> _jobChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundOperationWorker> _logger;

    public BackgroundOperationWorker(
        Channel<OperationJob> jobChannel,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundOperationWorker> logger)
    {
        _jobChannel = jobChannel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Operation Worker is starting.");

        await ProcessQueueAsync(stoppingToken);

        _logger.LogInformation("Background Operation Worker is stopping.");
    }

    public async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _jobChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job {JobId}", job.Id);
            }
        }
    }

    private async Task ProcessJobAsync(OperationJob job, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<IOperationAuditService>();
        var registry = scope.ServiceProvider.GetRequiredService<IOperationRegistry>();

        // Reload job from DB to get latest state (might have been cancelled)
        var currentJob = await dbContext.OperationJobs.FindAsync(new object[] { job.Id }, stoppingToken);
        if (currentJob == null || currentJob.State == OperationState.Cancelled)
            return;

        currentJob.State = OperationState.Running;
        currentJob.StartedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(stoppingToken);

        await auditService.RecordStartAsync(currentJob.Id, null, "System");

        try
        {
            var targetIds = JsonSerializer.Deserialize<System.Collections.Generic.List<Guid>>(currentJob.TargetRouterIds) 
                            ?? new System.Collections.Generic.List<Guid>();

            var operation = registry.ResolveOperation(currentJob.OperationType);
            
            var context = new OperationExecutionContext { JobId = currentJob.Id };
            
            // Execute
            var result = await operation.ExecuteAsync(context, stoppingToken);

            currentJob.State = result.IsSuccess ? OperationState.Completed : OperationState.Failed;
            currentJob.ResultMessage = result.Message;
            currentJob.Progress = 100;
        }
        catch (Exception ex)
        {
            currentJob.State = OperationState.Failed;
            currentJob.ResultMessage = ex.Message;
        }
        finally
        {
            currentJob.CompletedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(stoppingToken);

            var finalStatus = currentJob.State == OperationState.Completed ? "Success" : "Failed";
            await auditService.RecordCompletionAsync(currentJob.Id, null, finalStatus, currentJob.ResultMessage);
        }
    }
}
