using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Entities.Telemetry;
using MikroTikVoucherPrinter.Domain.Interfaces.Telemetry;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Repositories.Telemetry;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly PlatformDbContext _context;

    public TelemetryRepository(PlatformDbContext context)
    {
        _context = context;
    }

    public async Task StoreSnapshotAsync(DeviceTelemetrySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _context.DeviceTelemetry.AddAsync(snapshot, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task StoreInterfaceSnapshotsAsync(IEnumerable<InterfaceTelemetrySnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        await _context.InterfaceTelemetry.AddRangeAsync(snapshots, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task StoreAlertCandidateAsync(AlertCandidate candidate, CancellationToken cancellationToken = default)
    {
        await _context.AlertCandidates.AddAsync(candidate, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeviceTelemetrySnapshot?> GetLatestDeviceSnapshotAsync(Guid routerId, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceTelemetry
            .AsNoTracking()
            .Where(t => t.RouterId == routerId)
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<DeviceTelemetrySnapshot>> GetDeviceSnapshotsAsync(Guid routerId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.DeviceTelemetry
            .AsNoTracking()
            .Where(t => t.RouterId == routerId && t.Timestamp >= from && t.Timestamp <= to)
            .OrderBy(t => t.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task PurgeOldDataAsync(DateTime before, CancellationToken cancellationToken = default)
    {
        // Execute raw SQL to bypass memory tracking during massive purges
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM DeviceTelemetry WHERE Timestamp < {0}",
            new object[] { before }, cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM InterfaceTelemetry WHERE Timestamp < {0}",
            new object[] { before }, cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM AlertCandidates WHERE Timestamp < {0}",
            new object[] { before }, cancellationToken);
    }

    public async Task<(long DeviceSnapshots, long InterfaceSnapshots)> GetStorageStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var deviceCount = await _context.DeviceTelemetry.LongCountAsync(cancellationToken);
        var interfaceCount = await _context.InterfaceTelemetry.LongCountAsync(cancellationToken);
        return (deviceCount, interfaceCount);
    }
}
