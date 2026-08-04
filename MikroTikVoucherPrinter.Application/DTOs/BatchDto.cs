using MikroTikVoucherPrinter.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// DTO لعرض معلومات الدفعة في الواجهة
/// </summary>
public class BatchDto : ObservableObject
{
    public Guid   Id          { get; set; }
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public string CreatedBy   { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // ─── العدادات ──────────────────────────────────────────
    public int TotalCards     { get; set; }
    public int GeneratedCards { get; set; }
    public int SyncedCards    { get; set; }
    public int FailedCards    { get; set; }
    public int PrintedCards   { get; set; }

    // ─── الحالات ───────────────────────────────────────────
    public BatchStatus      Status      { get; set; }
    public BatchSyncStatus  SyncStatus  { get; set; }
    public BatchPrintStatus PrintStatus { get; set; }

    // ─── PDF ───────────────────────────────────────────────
    public string? PdfPath { get; set; }
    public string? PdfHash { get; set; }

    // ─── الأخطاء ───────────────────────────────────────────
    public string? LastError  { get; set; }
    public int     RetryCount { get; set; }

    // ─── التواريخ ──────────────────────────────────────────
    public DateTime? StartedAt    { get; set; }
    public DateTime? CompletedAt  { get; set; }
    public DateTime? CancelledAt  { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public DateTime? LastPrintTime { get; set; }

    // ─── خصائص العرض المحسوبة ──────────────────────────────

    public double SyncProgressPercent =>
        TotalCards > 0 ? (double)(SyncedCards + FailedCards) / TotalCards * 100 : 0;

    public double SyncSuccessPercent =>
        TotalCards > 0 ? (double)SyncedCards / TotalCards * 100 : 0;

    public double PrintProgressPercent =>
        TotalCards > 0 ? (double)PrintedCards / TotalCards * 100 : 0;

    public bool HasPdf    => !string.IsNullOrEmpty(PdfPath);
    public bool HasErrors => FailedCards > 0;

    public string StatusText => Status switch
    {
        BatchStatus.Created         => "تم الإنشاء",
        BatchStatus.Generating      => "جاري التوليد",
        BatchStatus.Generated       => "تم التوليد",
        BatchStatus.Syncing         => "جاري المزامنة",
        BatchStatus.Synced          => "تمت المزامنة",
        BatchStatus.Printing        => "جاري الطباعة",
        BatchStatus.Completed       => "مكتملة",
        BatchStatus.PartiallyFailed => "مكتملة جزئياً",
        BatchStatus.Failed          => "فشلت",
        BatchStatus.Cancelled       => "ملغاة",
        BatchStatus.Archived        => "مؤرشفة",
        _                           => "غير معروف"
    };

    public string SyncStatusText => SyncStatus switch
    {
        BatchSyncStatus.Pending         => "في الانتظار",
        BatchSyncStatus.InProgress      => "جاري المزامنة",
        BatchSyncStatus.Completed       => "مكتملة",
        BatchSyncStatus.PartiallyFailed => "جزئياً",
        BatchSyncStatus.Failed          => "فشلت",
        BatchSyncStatus.Retrying        => "إعادة محاولة",
        BatchSyncStatus.Paused          => "متوقفة",
        _                               => "غير معروف"
    };

    public string PrintStatusText => PrintStatus switch
    {
        BatchPrintStatus.NotStarted => "لم تُطبع",
        BatchPrintStatus.Generating => "جاري التوليد",
        BatchPrintStatus.Generated  => "PDF جاهز",
        BatchPrintStatus.Printed    => "مطبوعة",
        BatchPrintStatus.Failed     => "فشل الطباعة",
        _                           => "غير معروف"
    };

    /// <summary>ملخص للعرض: مزامنة 480/500 (20 فاشلة)</summary>
    public string SyncSummaryText
    {
        get
        {
            if (TotalCards == 0) return "لا كروت";
            var text = $"✅ {SyncedCards}/{TotalCards}";
            if (FailedCards > 0) text += $" ❌ {FailedCards}";
            return text;
        }
    }

    /// <summary>الوقت المنقضي منذ الإنشاء</summary>
    public string AgeText
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt;
            if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
            if (diff.TotalHours < 24)  return $"منذ {(int)diff.TotalHours} ساعة";
            return $"منذ {(int)diff.TotalDays} يوم";
        }
    }

    /// <summary>هل يمكن مزامنة هذه الدفعة؟</summary>
    public bool CanSync =>
        Status is BatchStatus.Generated or BatchStatus.PartiallyFailed &&
        SyncStatus is not BatchSyncStatus.InProgress;

    /// <summary>هل يمكن استكمال المزامنة؟</summary>
    public bool CanResume =>
        SyncStatus is BatchSyncStatus.Paused or BatchSyncStatus.PartiallyFailed &&
        Status is not BatchStatus.Cancelled and not BatchStatus.Archived;

    /// <summary>هل يمكن إعادة محاولة الكروت الفاشلة؟</summary>
    public bool CanRetry =>
        FailedCards > 0 &&
        Status is not BatchStatus.Cancelled and not BatchStatus.Archived;

    /// <summary>هل يمكن الطباعة؟</summary>
    public bool CanPrint =>
        SyncedCards > 0 &&
        Status is not BatchStatus.Cancelled and not BatchStatus.Archived;

    /// <summary>هل يمكن إعادة الطباعة؟</summary>
    public bool CanReprint => HasPdf && CanPrint;

    /// <summary>هل يمكن الحذف؟</summary>
    public bool CanDelete =>
        Status is not BatchStatus.Syncing and not BatchStatus.Generating;

    /// <summary>هل يمكن الأرشفة؟</summary>
    public bool CanArchive =>
        Status is BatchStatus.Completed or BatchStatus.PartiallyFailed or BatchStatus.Failed &&
        Status is not BatchStatus.Archived;
}
