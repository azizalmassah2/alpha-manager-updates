using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities.Telemetry;

namespace MikroTikVoucherPrinter.Domain.Interfaces.Telemetry;

public interface ITelemetryRepository
{
    Task StoreSnapshotAsync(DeviceTelemetrySnapshot snapshot, CancellationToken cancellationToken = default);
    Task StoreInterfaceSnapshotsAsync(IEnumerable<InterfaceTelemetrySnapshot> snapshots, CancellationToken cancellationToken = default);
    Task StoreAlertCandidateAsync(AlertCandidate candidate, CancellationToken cancellationToken = default);
    
    Task<DeviceTelemetrySnapshot?> GetLatestDeviceSnapshotAsync(Guid routerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DeviceTelemetrySnapshot>> GetDeviceSnapshotsAsync(Guid routerId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    
    Task PurgeOldDataAsync(DateTime before, CancellationToken cancellationToken = default);
    Task<(long DeviceSnapshots, long InterfaceSnapshots)> GetStorageStatisticsAsync(CancellationToken cancellationToken = default);
}
