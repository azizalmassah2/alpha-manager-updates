using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace Lux.Platform.Abstractions.Interfaces;

public interface IDeviceBackupProvider
{
    bool CanHandle(IDevice device);
    Task<Result<DeviceBackup>> CreateBackupAsync(IDevice device, BackupType backupType = BackupType.Configuration, CancellationToken cancellationToken = default);
    Task<Result> RestoreBackupAsync(IDevice device, DeviceBackup backup, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DeviceBackup>>> GetBackupsAsync(IDevice device, CancellationToken cancellationToken = default);
}
