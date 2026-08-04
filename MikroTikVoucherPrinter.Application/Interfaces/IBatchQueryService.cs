using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// خدمة الاستعلام عن الدفعات — للعرض في الواجهة
/// </summary>
public interface IBatchQueryService
{
    /// <summary>جلب كل الدفعات مرتبة بالأحدث أولاً</summary>
    Task<IReadOnlyList<BatchDto>> GetAllBatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>جلب دفعة محددة بتفاصيلها الكاملة</summary>
    Task<BatchDto?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>جلب كروت دفعة محددة كـ DTOs للعرض</summary>
    Task<IReadOnlyList<VoucherDto>> GetBatchVouchersAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>جلب الدفعات التي تحتوي على كروت فاشلة — للتنبيه في الواجهة</summary>
    Task<IReadOnlyList<BatchDto>> GetBatchesWithFailedSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>جلب الدفعات النشطة (جاري معالجتها الآن)</summary>
    Task<IReadOnlyList<BatchDto>> GetActiveBatchesAsync(CancellationToken cancellationToken = default);
}
