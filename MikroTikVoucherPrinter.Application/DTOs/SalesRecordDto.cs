using System;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// بيانات صف واحد في شاشة المبيعات
/// المصدر: userprofile.activated > 0 (أول تفعيل حقيقي للكرت)
/// </summary>
public class SalesRecordDto
{
    // ── المعرف الفريد ──────────────────────────────────────────
    public int Id { get; set; }

    // ── حقل المبيع الرسمي (First Activation) ──────────────────
    /// <summary>userprofile.activated → أول استخدام فعلي للكرت</summary>
    public long ActivatedUnix { get; set; }
    public DateTime ActivationDate => DateTimeOffset.FromUnixTimeSeconds(ActivatedUnix).LocalDateTime;
    public string ActivationDateText => ActivationDate.ToString("yyyy/MM/dd HH:mm");

    // ── بيانات الكرت ──────────────────────────────────────────
    /// <summary>user.userName (BLOB → string)</summary>
    public string VoucherCode { get; set; } = string.Empty;

    /// <summary>profile.name</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>userprofile.price (فلس / ريال)</summary>
    public long PriceRaw { get; set; }
    public string PriceText => PriceRaw > 0 ? $"{(PriceRaw / 100.0):N0}" : "—";

    // ── الحالة ────────────────────────────────────────────────
    /// <summary>userprofile.state: 0=جديد، 1=نشط، 2=منتهي</summary>
    public int State { get; set; }

    /// <summary>userprofile.paused</summary>
    public bool IsPaused { get; set; }

    public long UptimeLimit { get; set; }
    public long DownloadLimit { get; set; }
    public long UploadLimit { get; set; }
    public long TransferLimit { get; set; }

    public bool IsQuotaConsumed
    {
        get
        {
            bool hasLimits = UptimeLimit > 0 || TransferLimit > 0 || DownloadLimit > 0 || UploadLimit > 0;
            if (!hasLimits)
            {
                return false; 
            }
            
            if (UptimeLimit > 0 && UptimeUsedSeconds >= UptimeLimit) return true;
            if (TransferLimit > 0 && (DownloadUsedBytes + UploadUsedBytes) >= TransferLimit) return true;
            if (DownloadLimit > 0 && DownloadUsedBytes >= DownloadLimit) return true;
            if (UploadLimit > 0 && UploadUsedBytes >= UploadLimit) return true;
            
            return false;
        }
    }

    public int EffectiveState => IsQuotaConsumed ? 2 : 1;

    public string StatusText => IsQuotaConsumed ? "منتهي" : "نشط";

    // ── آخر ظهور ──────────────────────────────────────────────
    /// <summary>user.lastSeenAt (Unix timestamp)</summary>
    public long LastSeenAtUnix { get; set; }
    public string LastSeenText => LastSeenAtUnix > 0
        ? DateTimeOffset.FromUnixTimeSeconds(LastSeenAtUnix).LocalDateTime.ToString("yyyy/MM/dd HH:mm")
        : "—";

    // ── بيانات الاستهلاك ──────────────────────────────────────
    /// <summary>user.uptimeUsed (ثوانٍ)</summary>
    public long UptimeUsedSeconds { get; set; }
    public string UptimeUsedText => FormatSeconds(UptimeUsedSeconds);

    /// <summary>user.downloadUsed (bytes)</summary>
    public long DownloadUsedBytes { get; set; }
    public string DownloadUsedText => FormatBytes(DownloadUsedBytes);

    /// <summary>user.uploadUsed (bytes)</summary>
    public long UploadUsedBytes { get; set; }
    public string UploadUsedText => FormatBytes(UploadUsedBytes);

    // ── مساعدات تنسيق ─────────────────────────────────────────
    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "—";
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024L * 1024 * 1024):N1} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024 * 1024):N1} MB";
        if (bytes >= 1024) return $"{bytes / 1024:N1} KB";
        return $"{bytes} B";
    }

    private static string FormatSeconds(long seconds)
    {
        if (seconds <= 0) return "—";
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}ي {ts.Hours:D2}:{ts.Minutes:D2}";
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
