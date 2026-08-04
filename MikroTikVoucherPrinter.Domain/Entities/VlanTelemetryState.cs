using System;
using MikroTikVoucherPrinter.Domain.Common;

namespace MikroTikVoucherPrinter.Domain.Entities;

/// <summary>
/// حالة وتراكم استهلاك الفيلان عبر الزمن لمنع التصفير عند إعادة التشغيل
/// </summary>
public class VlanTelemetryState : BaseEntity
{
    public Guid RouterId { get; set; }
    public string VlanName { get; set; } = string.Empty;

    /// <summary>إجمالي الاستهلاك التراكمي السابق (Rx / Download)</summary>
    public long CumulativeRxBytes { get; set; }

    /// <summary>إجمالي الاستهلاك التراكمي السابق (Tx / Upload)</summary>
    public long CumulativeTxBytes { get; set; }

    /// <summary>آخر قراءة خام قُرأت من المايكروتك (Rx)</summary>
    public long LastRawRxBytes { get; set; }

    /// <summary>آخر قراءة خام قُرأت من المايكروتك (Tx)</summary>
    public long LastRawTxBytes { get; set; }

    /// <summary>تاريخ ووقت آخر عينة قُرأت من المايكروتك</summary>
    public DateTime LastSampleTime { get; set; } = DateTime.UtcNow;

    /// <summary>عدد مرات إعادات التشغيل المكتشفة لهذا الفيلان</summary>
    public int RebootCount { get; set; }
}
