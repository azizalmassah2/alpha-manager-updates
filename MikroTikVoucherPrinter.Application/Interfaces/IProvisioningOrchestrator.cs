using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IProvisioningOrchestrator
{
    Task<Result<DeviceProvisioningResult>> ProvisionDeviceAsync(
        IDevice device,
        ProvisioningTemplate template,
        IReadOnlyDictionary<string, string>? customVariables = null,
        CancellationToken cancellationToken = default);
}
