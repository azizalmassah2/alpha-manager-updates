using System;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// بيانات الكرت للعرض في الواجهة
/// </summary>
public class VoucherDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;

    public VoucherStatus Status { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>مصدر السجل في العرض (محلي / دمج مع الراوتر).</summary>
    public VoucherDataOrigin DataOrigin { get; set; } = VoucherDataOrigin.Local;

    public string DataOriginText => DataOrigin switch
    {
        VoucherDataOrigin.Local => "محلي",
        VoucherDataOrigin.RouterMerged => "محلي + راوتر",
        VoucherDataOrigin.RouterOnly => "راوتر فقط",
        _ => ""
    };

    /// <summary>هل البروفايل فارغ أو قيمة شكلية (لا باقة فعلية).</summary>
    public bool HasNoProfile => LooksLikeMissingProfile(Profile);

    public static bool LooksLikeMissingProfile(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile)) return true;
        var p = profile.Trim();
        if (p is "-" or "_" or "." or "—") return true;
        if (p.Equals("none", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.Equals("n/a", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.Equals("null", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>تلميح استهلاك الرصيد (بايت) من آخر جلب للراوتر.</summary>
    public long? QuotaUsedBytes { get; set; }

    public long? QuotaLimitBytes { get; set; }

    /// <summary>يُضبط عند وجود حد بايت ووصول الاستهلاك إليه (Hotspot / User Manager).</summary>
    public bool IsQuotaDepleted { get; set; }

    public long? DownloadUsedBytes { get; set; }
    public long? UploadUsedBytes { get; set; }

    public string DownloadUsedText => DownloadUsedBytes.HasValue ? FormatBytes(DownloadUsedBytes.Value) : "—";
    public string UploadUsedText => UploadUsedBytes.HasValue ? FormatBytes(UploadUsedBytes.Value) : "—";

    /// <summary>مدة / صلاحية من الراوتر إن وُجدت (نص خام).</summary>
    public string? RouterTimeHint { get; set; }

    public string StatusText => IsDeleted
        ? "محذوف"
        : IsDisabled
            ? "معطل"
            : Status switch
    {
        VoucherStatus.Unused => "غير مستخدم",
        VoucherStatus.Used => "مُستخدم",
        VoucherStatus.Expired => "منتهي",
        _ => "غير معروف"
    };

    public SyncStatus SyncStatus { get; set; }
    public string SyncStatusText => SyncStatus switch
    {
        SyncStatus.Pending => "قيد الانتظار",
        SyncStatus.Synced => "متزامن",
        SyncStatus.Failed => "فشل المزامنة",
        _ => "غير معروف"
    };

    public DateTime CreatedAt { get; set; }
    public Guid BatchId { get; set; }
    public decimal Price { get; set; }
    public string? AgentName { get; set; }

    public string SerialNumber => Id.ToString()[..8].ToUpper();
    public DateTime? UsedAt => Status == VoucherStatus.Used ? (ImportDate ?? CreatedAt) : null;

    public CredentialMode CredentialMode { get; set; } = CredentialMode.UsernameAndPassword;

    // تتبع أصل الكرت (مستورد أم مولد محلياً)
    public VoucherSource VoucherSource { get; set; } = VoucherSource.GeneratedByLux;
    public DateTime? ImportDate { get; set; }
    public string? CreatedBy { get; set; } = "Lux System";
    public string? Comment { get; set; }

    public string QuotaSummaryText
    {
        get
        {
            if (QuotaLimitBytes is > 0 && QuotaUsedBytes.HasValue)
                return $"{FormatBytes(QuotaUsedBytes.Value)} / {FormatBytes(QuotaLimitBytes.Value)}";
            if (QuotaUsedBytes is > 0)
                return $"مستخدم {FormatBytes(QuotaUsedBytes.Value)}";
            return "—";
        }
    }

    private static string FormatBytes(long b)
    {
        if (b >= 1024L * 1024 * 1024) return $"{b / (1024L * 1024 * 1024)} GB";
        if (b >= 1024 * 1024) return $"{b / (1024 * 1024)} MB";
        if (b >= 1024) return $"{b / 1024} KB";
        return $"{b} B";
    }
}
