using System;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Entities;

public class Voucher : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Relational
    public string ProfileName { get; set; } = string.Empty; // Direct mapping to MikroTik Profile
    public Guid BatchId { get; set; }

    /// <summary>
    /// طريقة الاعتماد — تتحكم في توليد وطباعة وإرسال البيانات
    /// </summary>
    public CredentialMode CredentialMode { get; set; } = CredentialMode.UsernameAndPassword;

    /// <summary>
    /// كلمة السر الفعلية المرسلة للمايكروتك — محسوبة حسب الـ CredentialMode
    /// </summary>
    public string? EffectivePassword => CredentialMode switch
    {
        CredentialMode.UsernameOnly        => null,
        CredentialMode.UsernameEqualsPassword => Username,
        CredentialMode.UsernameAndPassword => Password,
        _                                  => Password
    };

    // Status
    public VoucherStatus Status { get; set; } = VoucherStatus.Unused;
    public SyncStatus SyncStatus { get; private set; } = SyncStatus.Pending;
    public VoucherPrintStatus PrintStatus { get; set; } = VoucherPrintStatus.Reserved;
    public string? MikroTikUserId { get; private set; }
    public string? SyncError { get; private set; }
    public DateTime? SyncedAt { get; private set; }

    // ربط الوكيل (اختياري)
    public Guid? AgentId { get; set; }
    public virtual Agent? Agent { get; set; }
    
    // ربط الراوتر
    public Guid RouterId { get; set; }

    // تتبع أصل الكرت (مستورد أم مولد محلياً)
    public VoucherSource VoucherSource { get; set; } = VoucherSource.GeneratedByLux;
    public DateTime? ImportDate { get; set; }
    public string? CreatedBy { get; set; } = "Lux System";
    public string? Comment { get; set; }

    public bool IsDisabled { get; set; }
    public bool IsFavorite { get; set; }
    public long BytesUsed { get; set; }
    public long DownloadUsedBytes { get; set; }
    public long UploadUsedBytes { get; set; }
    public long UptimeUsedSeconds { get; set; }

    // Recycle Bin Metadata
    public DateTime? DeletedDate { get; set; }
    public VoucherDeletedSource? DeletedSource { get; set; }

    // Navigation
    public virtual Batch Batch { get; set; } = null!;

    // ==========================================
    // Workflow Engine: Sync Status Transitions
    // ==========================================

    public void MarkAsSynced(string mikrotikUserId)
    {
        if (string.IsNullOrWhiteSpace(mikrotikUserId))
            throw new ArgumentException("الرقم التعريفي من سيرفر المايكروتيك يجب أن يكون صالحاً", nameof(mikrotikUserId));

        if (SyncStatus == SyncStatus.Synced)
            return; // بالفعل متزامن

        SyncStatus = SyncStatus.Synced;
        MikroTikUserId = mikrotikUserId;
        SyncedAt = DateTime.UtcNow;
        SyncError = null;
    }

    public void MarkAsFailed(string error)
    {
        if (SyncStatus == SyncStatus.Synced)
            throw new InvalidOperationException("لا يمكن تعليم كرت بأنه فاشل للمزامنة وهو بالأصل متزامن.");

        SyncStatus = SyncStatus.Failed;
        SyncError = error;
    }

    public void MarkAsPending()
    {
        if (SyncStatus == SyncStatus.Synced)
            throw new InvalidOperationException("لا يمكن إعادة الكرت لوضعية Pending بعد مزامنته بنجاح.");

        SyncStatus = SyncStatus.Pending;
        SyncError = null;
    }

    public void MarkAsPendingForDeleteOrRestore()
    {
        SyncStatus = SyncStatus.Pending;
        SyncError = null;
    }

    public void MarkAsSyncedForDelete()
    {
        SyncStatus = SyncStatus.Synced;
        SyncError = null;
        SyncedAt = DateTime.UtcNow;
    }
}
