using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions.Common;

namespace Lux.OpenWrt.Interfaces;

public interface IBackupRestoreService
{
    Task<Result<DeviceBackup>> CreateBackupAsync(string ip, string session, string host, BackupType backupType, string deviceName = "Unknown OpenWrt", CancellationToken cancellationToken = default);
    Task<Result<bool>> RestoreBackupAsync(string ip, string session, string backupFilePath, string expectedChecksum, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DeviceBackup>>> GetBackupsAsync(string host, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);
}
