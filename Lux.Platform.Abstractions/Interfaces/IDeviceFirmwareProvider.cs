using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace Lux.Platform.Abstractions.Interfaces;

public interface IDeviceFirmwareProvider
{
    bool CanHandle(IDevice device);

    Task<Result<FirmwareUpgradeResult>> UpgradeAsync(
        IDevice device,
        FirmwareImage image,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GetCurrentVersionAsync(
        IDevice device,
        CancellationToken cancellationToken = default);

    Task<Result<FirmwareCompatibilityResult>> ValidateFirmwareAsync(
        IDevice device,
        FirmwareImage image,
        CancellationToken cancellationToken = default);

    string GetRemoteUploadPath(IDevice device, FirmwareImage image);
}
