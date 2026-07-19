using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.OpenWrt.Interfaces;

public interface IOpenWrtDeviceManager
{
    /// <summary>
    /// ظٹظپط­طµ ط§ظ„ط¬ظ‡ط§ط² ظˆظٹطھطµظ„ ط¨ظ‡ ظ„ظ„طھط­ظ‚ظ‚ ظ…ظ† ط¨ظٹط§ظ†ط§طھظ‡ ظˆط§ظƒطھط´ط§ظپ ط¥ط¹ط¯ط§ط¯ط§طھظ‡
    /// </summary>
    Task<Result<NetworkDevice>> DiscoverDeviceAsync(string host, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// ظٹطھط£ظƒط¯ ظ…ظ† ط¥ظ…ظƒط§ظ†ظٹط© ط§ظ„ظˆطµظˆظ„ ط¥ظ„ظ‰ ط§ظ„ط¬ظ‡ط§ط² (Reachable) ط¨ط§ط³طھط®ط¯ط§ظ… Ping ط¨ط³ظٹط· ط£ظˆ ط§طھطµط§ظ„ HTTP
    /// </summary>
    Task<bool> IsReachableAsync(string host, CancellationToken cancellationToken = default);

    Task<Result<DeviceBackup>> CreateBackupAsync(string host, string username, string password, BackupType backupType, CancellationToken cancellationToken = default);
    Task<Result<bool>> RestoreBackupAsync(string host, string username, string password, string backupFilePath, string expectedChecksum, CancellationToken cancellationToken = default);
}
