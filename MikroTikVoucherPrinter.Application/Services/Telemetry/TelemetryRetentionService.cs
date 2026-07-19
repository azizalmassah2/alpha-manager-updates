using System;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Interfaces.Telemetry;
using MikroTikVoucherPrinter.Domain.Interfaces.Telemetry;

namespace MikroTikVoucherPrinter.Application.Services.Telemetry;

public class TelemetryRetentionService : ITelemetryRetentionService
{
    private readonly ITelemetryRepository _telemetryRepository;

    public TelemetryRetentionService(ITelemetryRepository telemetryRepository)
    {
        _telemetryRepository = telemetryRepository;
    }

    public async Task PurgeOldDataAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        var beforeDate = DateTime.UtcNow.AddDays(-retentionDays);
        await _telemetryRepository.PurgeOldDataAsync(beforeDate, cancellationToken);
    }

    public async Task<(long DeviceSnapshots, long InterfaceSnapshots)> GetStorageStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await _telemetryRepository.GetStorageStatisticsAsync(cancellationToken);
    }
}
