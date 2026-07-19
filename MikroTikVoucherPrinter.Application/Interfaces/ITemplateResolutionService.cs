using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface ITemplateResolutionService
{
    Task<Result<DeviceConfiguration>> ResolveTemplateAsync(
        ProvisioningTemplate template, 
        IDevice device, 
        IDictionary<string, string>? additionalVariables = null, 
        CancellationToken cancellationToken = default);
}
