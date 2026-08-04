using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Entities;

/// <summary>
/// الوحدة الأساسية للنظام — كل Voucher ينتمي إلى Batch.
/// تمثل الدفعة دورة حياة كاملة: توليد → مزامنة → طباعة.
/// </summary>
public class Batch : BaseEntity
{
    // ─── الهوية ────────────────────────────────────────────
    /// <summary>اسم الدفعة المعروض للمستخدم</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>وصف اختياري للدفعة</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>المستخدم أو النظام الذي أنشأ الدفعة</summary>
    public string CreatedBy { get; set; } = "Lux System";

    /// <summary>الراوتر المرتبط بهذه الدفعة</summary>
    public Guid RouterId { get; set; }

    // ─── الباقة ────────────────────────────────────────────
    /// <summary>اسم الباقة المرتبطة بكروت هذه الدفعة</summary>
    public string ProfileName { get; set; } = string.Empty;

    // ─── العدادات ──────────────────────────────────────────
    /// <summary>العدد الإجمالي المطلوب توليده</summary>
    public int TotalCards { get; set; }

    /// <summary>عدد الكروت التي تم توليدها فعلياً</summary>
    public int GeneratedCards { get; set; }

    /// <summary>عدد الكروت التي تمت مزامنتها بنجاح</summary>
    public int SyncedCards { get; set; }

    /// <summary>عدد الكروت التي فشلت في المزامنة</summary>
    public int FailedCards { get; set; }

    /// <summary>عدد الكروت التي تمت طباعتها</summary>
    public int PrintedCards { get; set; }

    // ─── حالة الدفعة ───────────────────────────────────────
    /// <summary>حالة دورة الحياة الإجمالية للدفعة</summary>
    public BatchStatus Status { get; set; } = BatchStatus.Created;

    /// <summary>حالة المزامنة على مستوى الدفعة</summary>
    public BatchSyncStatus SyncStatus { get; set; } = BatchSyncStatus.Pending;

    /// <summary>حالة الطباعة / PDF على مستوى الدفعة</summary>
    public BatchPrintStatus PrintStatus { get; set; } = BatchPrintStatus.NotStarted;

    // ─── تتبع PDF ──────────────────────────────────────────
    /// <summary>المسار الكامل لملف PDF المولّد</summary>
    public string? PdfPath { get; set; }

    /// <summary>Hash لملف PDF للتحقق من سلامته</summary>
    public string? PdfHash { get; set; }

    // ─── معالجة الأخطاء ────────────────────────────────────
    /// <summary>رسالة الخطأ الحالية إن وُجدت</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>آخر خطأ حدث أثناء العمليات</summary>
    public string? LastError { get; set; }

    /// <summary>عدد مرات إعادة المحاولة</summary>
    public int RetryCount { get; set; }

    // ─── التواريخ التشغيلية ────────────────────────────────
    /// <summary>وقت بدء أول عملية (توليد / مزامنة)</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>وقت اكتمال الدفعة بالكامل</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>وقت إلغاء الدفعة إذا أُلغيت</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>آخر وقت تمت فيه مزامنة</summary>
    public DateTime? LastSyncTime { get; set; }

    /// <summary>آخر وقت تمت فيه طباعة</summary>
    public DateTime? LastPrintTime { get; set; }

    // ─── Metadata ──────────────────────────────────────────
    /// <summary>بيانات إضافية بصيغة JSON (للاستخدام المستقبلي)</summary>
    public string? Metadata { get; set; }

    // ─── Navigation ────────────────────────────────────────
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();

    // ─── Computed Helpers ──────────────────────────────────
    /// <summary>هل الدفعة قابلة للمزامنة؟</summary>
    public bool CanSync =>
        Status is BatchStatus.Generated or BatchStatus.PartiallyFailed &&
        SyncStatus is not BatchSyncStatus.InProgress;

    /// <summary>هل الدفعة قابلة لاستكمال المزامنة؟</summary>
    public bool CanResume =>
        SyncStatus is BatchSyncStatus.Paused or BatchSyncStatus.PartiallyFailed &&
        Status is not BatchStatus.Cancelled and not BatchStatus.Archived;

    /// <summary>هل الدفعة قابلة للطباعة؟</summary>
    public bool CanPrint =>
        SyncedCards > 0 &&
        Status is not BatchStatus.Cancelled and not BatchStatus.Archived;

    /// <summary>هل يوجد PDF صالح؟</summary>
    public bool HasValidPdf =>
        !string.IsNullOrEmpty(PdfPath) && File.Exists(PdfPath);

    /// <summary>نسبة تقدم المزامنة</summary>
    public double SyncProgressPercent =>
        TotalCards > 0 ? (double)(SyncedCards + FailedCards) / TotalCards * 100 : 0;
}
