using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace Lux.Platform.Abstractions.Interfaces;

public interface IDeviceConfigurationProvider
{
    bool CanHandle(IDevice device);
    Task<Result> ApplyConfigurationAsync(IDevice device, DeviceConfiguration configuration, CancellationToken cancellationToken = default);
    Task<Result<DeviceConfiguration>> ExportConfigurationAsync(IDevice device, CancellationToken cancellationToken = default);
    Task<Result<ConfigurationValidationResult>> ValidateConfigurationAsync(DeviceConfiguration configuration, CancellationToken cancellationToken = default);
}
