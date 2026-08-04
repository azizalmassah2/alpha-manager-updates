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
    /// إضافة آلاف الطلبات في دفعة واحدة بدون N+1 وبأداء عالٍ ومعالجة التكرار
    /// </summary>
    Task<BulkInsertResult> BulkInsertSafelyAsync(IEnumerable<Voucher> vouchers, CancellationToken cancellationToken = default);

    // ─── Batch-level Queries (استعلامات مباشرة — تجنب الذاكرة) ───────────────

    /// <summary>
    /// جلب كروت Pending لدفعة محددة مباشرة من قاعدة البيانات.
    /// يُستخدم بدلاً من GetPendingSyncAsync + Where في الذاكرة.
    /// </summary>
    Task<IReadOnlyList<Voucher>> GetPendingByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>جلب الكروت الفاشلة لدفعة محددة مباشرة من قاعدة البيانات</summary>
    Task<IReadOnlyList<Voucher>> GetFailedByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>عدد الكروت المزامنة بنجاح في دفعة محددة</summary>
    Task<int> GetSyncedCountByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
}
