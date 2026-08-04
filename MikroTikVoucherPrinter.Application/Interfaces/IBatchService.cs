using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// الواجهة المركزية لكل عمليات Batch.
/// الـ Batch هو وحدة العمل الأساسية في النظام.
/// </summary>
public interface IBatchService
{
    // ─── إنشاء ──────────────────────────────────────────────
    /// <summary>
    /// ينشئ Batch جديدة ويولّد كروتها ويحفظها في قاعدة البيانات.
    /// يُستدعى من GenerateVoucherPage فقط.
    /// يعيد BatchId الجديد.
    /// </summary>
    Task<Guid> CreateBatchAsync(
        CreateBatchRequest request,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    // ─── المزامنة ────────────────────────────────────────────
    /// <summary>يُزامن كروت الدفعة مع المايكروتك (الكروت Pending فقط)</summary>
    Task<SyncMetrics> SyncBatchAsync(
        Guid batchId,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// يعيد محاولة الكروت الفاشلة في هذه الدفعة فقط.
    /// يزيد Batch.RetryCount بمقدار 1.
    /// </summary>
    Task<SyncMetrics> RetryFailedBatchAsync(
        Guid batchId,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// يستكمل المزامنة من آخر نقطة توقف.
    /// يعالج الكروت Pending + Failed معاً.
    /// </summary>
    Task<SyncMetrics> ResumeBatchAsync(
        Guid batchId,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>يوقف المزامنة الجارية ويضبط Status = Paused</summary>
    Task CancelSyncAsync(Guid batchId, CancellationToken cancellationToken = default);

    // ─── الطباعة / PDF ───────────────────────────────────────
    /// <summary>
    /// يولّد PDF لكروت الدفعة ويحفظه ويحدّث Batch.PdfPath.
    /// يُستخدم للطباعة الأولى.
    /// </summary>
    Task<BatchPrintResult> PrintBatchAsync(
        Guid batchId,
        PrintSettingsDto? settings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// يُعيد توليد PDF لدفعة سبق طباعتها.
    /// يُلغي PdfPath القديم ويُنشئ ملفاً جديداً.
    /// </summary>
    Task<BatchPrintResult> ReprintBatchAsync(
        Guid batchId,
        PrintSettingsDto? settings = null,
        CancellationToken cancellationToken = default);

    // ─── الإدارة ──────────────────────────────────────────────
    /// <summary>
    /// يحذف الدفعة وجميع كروتها cascade.
    /// لا رجعة — يجب عرض تأكيد للمستخدم قبل الاستدعاء.
    /// </summary>
    Task DeleteBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>يؤرشف الدفعة — تصبح للقراءة فقط</summary>
    Task ArchiveBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>يفتح مجلد PDF في Explorer</summary>
    Task<bool> OpenPdfFolderAsync(Guid batchId, CancellationToken cancellationToken = default);
}
