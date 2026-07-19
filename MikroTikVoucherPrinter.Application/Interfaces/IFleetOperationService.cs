using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IFleetOperationService
{
    Task<Guid> StartProvisioningAsync(
        IReadOnlyCollection<IDevice> devices,
        ProvisioningTemplate template,
        CancellationToken cancellationToken = default);

    Task<Guid> StartBackupAsync(
        IReadOnlyCollection<IDevice> devices,
        CancellationToken cancellationToken = default);

    Task<Guid> StartRestoreAsync(
        IReadOnlyCollection<IDevice> devices,
        DeviceBackup backup,
        CancellationToken cancellationToken = default);

    Task<FleetOperation?> GetOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<OperationProgress> GetProgressAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FleetOperation>> GetOperationsAsync(
        CancellationToken cancellationToken = default);

    Task<Guid> StartFirmwareUpgradeAsync(IEnumerable<IDevice> devices, FirmwareImage image);

    Task CancelAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
}
