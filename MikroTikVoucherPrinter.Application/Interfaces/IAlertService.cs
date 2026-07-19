using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models.Monitoring;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IAlertService
{
    Task<IEnumerable<Alert>> GetActiveAlertsAsync();
    Task<IEnumerable<Alert>> GetAlertsByDeviceAsync(Guid deviceId);
    Task AcknowledgeAlertAsync(Guid alertId);
    Task EvaluateDeviceStateAsync(Lux.Platform.Abstractions.Models.DeviceState state);
    Task AddAlertAsync(Alert alert);
}
