using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IUnifiedFirmwareService
{
    Task<Result<FirmwareUpgradeResult>> UpgradeFirmwareAsync(
        IDevice device,
        FirmwareImage image,
        CancellationToken cancellationToken = default);

    Task<Result<FirmwareCompatibilityResult>> ValidateFirmwareAsync(
        IDevice device,
        FirmwareImage image,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GetCurrentVersionAsync(
        IDevice device,
        CancellationToken cancellationToken = default);
}
