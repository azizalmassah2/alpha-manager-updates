using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Interfaces;

/// <summary>
/// مستودع الدفعات — العمليات المتخصصة على مستوى Batch
/// </summary>
public interface IBatchRepository : IGenericRepository<Batch>
{
    /// <summary>جلب كل الدفعات مرتبة بالأحدث أولاً</summary>
    Task<IReadOnlyList<Batch>> GetAllBatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>جلب دفعة محددة مع كل كروتها</summary>
    Task<Batch?> GetBatchWithVouchersAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>عدد الكروت المعلقة (Pending) في الدفعة</summary>
    Task<int> GetPendingVoucherCountAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>عدد الكروت الفاشلة في الدفعة</summary>
    Task<int> GetFailedVoucherCountAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// إعادة حساب عدادات الدفعة من قاعدة البيانات وحفظها.
    /// يجب استدعاؤه بعد كل عملية Sync أو Print أو Delete.
    /// </summary>
    Task UpdateCountersAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>جلب الدفعات التي تحتوي على كروت فاشلة</summary>
    Task<IReadOnlyList<Batch>> GetBatchesWithFailedVouchersAsync(CancellationToken cancellationToken = default);

    /// <summary>جلب الدفعات النشطة (جاري معالجتها)</summary>
    Task<IReadOnlyList<Batch>> GetActiveBatchesAsync(CancellationToken cancellationToken = default);
}
