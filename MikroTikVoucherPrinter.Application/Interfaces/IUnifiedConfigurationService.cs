using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IUnifiedConfigurationService
{
    Task<Result> ApplyConfigurationAsync(IDevice device, DeviceConfiguration configuration, CancellationToken cancellationToken = default);
    Task<Result<DeviceConfiguration>> ExportConfigurationAsync(IDevice device, CancellationToken cancellationToken = default);
    Task<Result<ConfigurationValidationResult>> ValidateConfigurationAsync(IDevice device, DeviceConfiguration configuration, CancellationToken cancellationToken = default);
}
