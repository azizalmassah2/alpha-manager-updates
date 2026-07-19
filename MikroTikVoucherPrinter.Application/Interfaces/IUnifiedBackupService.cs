using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IUnifiedBackupService
{
    Task<Result<DeviceBackup>> CreateBackupAsync(IDevice device, BackupType backupType = BackupType.Configuration, CancellationToken cancellationToken = default);
    Task<Result> RestoreBackupAsync(IDevice device, DeviceBackup backup, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DeviceBackup>>> GetBackupsAsync(IDevice device, CancellationToken cancellationToken = default);
}
