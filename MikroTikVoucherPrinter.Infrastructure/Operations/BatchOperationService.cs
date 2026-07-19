using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Infrastructure.Operations;

public class BatchOperationService : IBatchOperationService
{
    private readonly IOperationEngine _engine;

    public BatchOperationService(IOperationEngine engine)
    {
        _engine = engine;
    }

    public async Task<Guid> ExecuteModemBatchRebootAsync(IEnumerable<Guid> modemIds)
    {
        return await _engine.QueueOperationAsync(OperationType.Reboot, DeviceRole.Modem, modemIds, "Batch Modem Reboot");
    }

    public async Task<Guid> ExecuteWirelessSignalCheckAsync(IEnumerable<Guid> wirelessDeviceIds)
    {
        return await _engine.QueueOperationAsync(OperationType.SignalCheck, DeviceRole.AccessPoint, wirelessDeviceIds, "Wireless Signal Check");
    }

    public async Task<Guid> ExecuteRouterBackupAsync()
    {
        // Null target for global backup
        return await _engine.QueueOperationAsync(OperationType.Backup, DeviceRole.CoreRouter, new List<Guid>(), "Core Router Backup");
    }
}
