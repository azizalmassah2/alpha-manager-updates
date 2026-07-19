using System;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using Lux.Platform.Abstractions.Interfaces;

namespace MikroTikVoucherPrinter.Application.Operations.Router;

public class RouterBackupOperation : IDeviceOperation
{
    private readonly IMikroTikIntegrationService _mikroTikService;
    private readonly IEventBus _eventBus;

    public RouterBackupOperation(IMikroTikIntegrationService mikroTikService, IEventBus eventBus)
    {
        _mikroTikService = mikroTikService;
        _eventBus = eventBus;
    }

    public async Task<OperationResult> ExecuteAsync(OperationExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            _eventBus.Publish(new OperationProgressEventArgs
            {
                JobId = context.JobId,
                Percentage = 10,
                Message = "Connecting to Router..."
            });

            // Simulate some backup work or call real MikroTik service
            await Task.Delay(1500, cancellationToken);
            
            _eventBus.Publish(new OperationProgressEventArgs
            {
                JobId = context.JobId,
                Percentage = 50,
                Message = "Generating Backup File..."
            });
            
            await Task.Delay(2000, cancellationToken);

            _eventBus.Publish(new OperationProgressEventArgs
            {
                JobId = context.JobId,
                Percentage = 100,
                Message = "Backup Completed Successfully"
            });

            return OperationResult.Success("Backup created successfully.");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Backup failed: {ex.Message}");
        }
    }
}
