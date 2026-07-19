using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Models.Telemetry;

namespace MikroTikVoucherPrinter.Application.Interfaces.Telemetry;

public interface IMonitoringEngine
{
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);
    Task StopMonitoringAsync(CancellationToken cancellationToken = default);
    
    Task MonitorDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task StopMonitoringDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    
    IEnumerable<PollingSession> GetMonitoringStatus();
}
