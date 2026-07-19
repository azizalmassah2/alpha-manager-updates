using System;
using MikroTikVoucherPrinter.Domain.Common;

namespace MikroTikVoucherPrinter.Domain.Entities.Platform;

public class SyncQueueItem : BaseEntity
{
    public Guid RouterId { get; set; }
    public Router Router { get; set; } = null!;

    public string OperationType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
}
