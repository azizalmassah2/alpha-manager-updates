using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces.Telemetry;

public interface ITelemetryRetentionService
{
    Task PurgeOldDataAsync(int retentionDays, CancellationToken cancellationToken = default);
    Task<(long DeviceSnapshots, long InterfaceSnapshots)> GetStorageStatisticsAsync(CancellationToken cancellationToken = default);
}
