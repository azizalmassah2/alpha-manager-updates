using System;
using MikroTikVoucherPrinter.Domain.Common;

namespace MikroTikVoucherPrinter.Domain.Entities;

public class PrintJobEvent : BaseEntity
{
    public Guid JobId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Info"; // Info, Warning, Error
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid RouterId { get; set; }

    // Navigation
    public virtual PrintJob? Job { get; set; }
}
