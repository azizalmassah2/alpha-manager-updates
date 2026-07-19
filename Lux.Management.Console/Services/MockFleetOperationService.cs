using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace Lux.Management.Console.Services;

public class MockFleetOperationService : IFleetOperationService
{
    public Task CancelAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<FleetOperation>> GetOperationsAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<FleetOperation>
        {
            new FleetOperation { Id = Guid.NewGuid(), Type = FleetOperationType.FirmwareUpgrade, Status = FleetOperationStatus.Running },
            new FleetOperation { Id = Guid.NewGuid(), Type = FleetOperationType.Backup, Status = FleetOperationStatus.Failed }
        };
        return Task.FromResult<IReadOnlyCollection<FleetOperation>>(list);
    }

    public Task<FleetOperation?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<FleetOperation?>(null);
    }

    public Task<OperationProgress> GetProgressAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OperationProgress());
    }

    public Task<Guid> StartBackupAsync(IReadOnlyCollection<IDevice> devices, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<Guid> StartRestoreAsync(IReadOnlyCollection<IDevice> devices, DeviceBackup backup, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<Guid> StartFirmwareUpgradeAsync(IEnumerable<IDevice> devices, FirmwareImage image)
    {
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<Guid> StartProvisioningAsync(IReadOnlyCollection<IDevice> devices, ProvisioningTemplate template, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Guid.NewGuid());
    }
}
