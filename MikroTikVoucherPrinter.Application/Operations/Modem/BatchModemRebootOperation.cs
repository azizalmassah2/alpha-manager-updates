using System;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using Lux.Platform.Abstractions.Interfaces;

namespace MikroTikVoucherPrinter.Application.Operations.Modem;

public class BatchModemRebootOperation : IDeviceOperation
{
    private readonly IEventBus _eventBus;

    public BatchModemRebootOperation(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task<OperationResult> ExecuteAsync(OperationExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            _eventBus.Publish(new OperationProgressEventArgs
            {
                JobId = context.JobId,
                Percentage = 20,
                Message = "Sending reboot command to modem..."
            });

            // Delay simulating network command
            await Task.Delay(2000, cancellationToken);
            
            _eventBus.Publish(new OperationProgressEventArgs
            {
                JobId = context.JobId,
                Percentage = 100,
                Message = "Reboot command accepted."
            });

            return OperationResult.Success("Modem rebooted successfully.");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Reboot failed: {ex.Message}");
        }
    }
}
