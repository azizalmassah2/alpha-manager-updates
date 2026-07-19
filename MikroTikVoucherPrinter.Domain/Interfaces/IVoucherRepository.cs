using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Entities;

namespace MikroTikVoucherPrinter.Domain.Interfaces;

public interface IVoucherRepository : IGenericRepository<Voucher>
{
    Task<Voucher?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Voucher>> GetPendingSyncAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Voucher>> GetFailedSyncAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// ط¥ط¶ط§ظپط© ط¢ظ„ط§ظپ ط§ظ„ط·ظ„ط¨ط§طھ ظپظٹ ط¯ظپط¹ط© ظˆط§ط­ط¯ط© ط¨ط¯ظˆظ† N+1 ظˆط¨ط£ط¯ط§ط، ط¹ط§ظ„ظٹ ظˆظ…ط¹ط§ظ„ط¬ط© ط§ظ„طھظƒط±ط§ط± 
    /// </summary>
    Task<BulkInsertResult> BulkInsertSafelyAsync(IEnumerable<Voucher> vouchers, CancellationToken cancellationToken = default);
}
