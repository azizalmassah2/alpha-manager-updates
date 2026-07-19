using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces.Operations;

/// <summary>
/// Worker interface that processes queued jobs.
/// Usually implemented by a BackgroundService / IHostedService.
/// </summary>
public interface IOperationWorker
{
    Task ProcessQueueAsync(CancellationToken stoppingToken);
}
