using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Application.Services;

public class UnifiedBackupService : IUnifiedBackupService
{
    private readonly IEnumerable<IDeviceBackupProvider> _providers;

    public UnifiedBackupService(IEnumerable<IDeviceBackupProvider> providers)
    {
        _providers = providers;
    }

    private IDeviceBackupProvider? GetProvider(IDevice device)
    {
        return _providers.FirstOrDefault(p => p.CanHandle(device));
    }

    public Task<Result<DeviceBackup>> CreateBackupAsync(IDevice device, BackupType backupType = BackupType.Configuration, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(device);
        if (provider == null)
        {
            return Task.FromResult(Result<DeviceBackup>.Failure($"No backup provider found for device type/vendor: {device.GetType().Name}", ErrorType.Validation));
        }

        return provider.CreateBackupAsync(device, backupType, cancellationToken);
    }

    public Task<Result> RestoreBackupAsync(IDevice device, DeviceBackup backup, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(device);
        if (provider == null)
        {
            return Task.FromResult(Result.Failure($"No backup provider found for device type/vendor: {device.GetType().Name}", ErrorType.Validation));
        }

        return provider.RestoreBackupAsync(device, backup, cancellationToken);
    }

    public Task<Result<IReadOnlyList<DeviceBackup>>> GetBackupsAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(device);
        if (provider == null)
        {
            return Task.FromResult(Result<IReadOnlyList<DeviceBackup>>.Failure($"No backup provider found for device type/vendor: {device.GetType().Name}", ErrorType.Validation));
        }

        return provider.GetBackupsAsync(device, cancellationToken);
    }
}
