using System;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Entities;

public class PrintJob : BaseEntity
{
    public Guid TemplateId { get; set; }
    public DateTime PrintedAt { get; set; } = DateTime.UtcNow;
    public int CardCount { get; set; }
    public Guid? BatchId { get; set; }
    public int OutputFormat { get; set; } = 0; // Standard A4 grid, etc.
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;
    public PrintJobStep CurrentStep { get; set; } = PrintJobStep.GeneratingVouchers;
    public string? OutputFilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid RouterId { get; set; }
    public string? JobParametersJson { get; set; }
    
    public int ReservedCount { get; set; }
    public int SyncedCount { get; set; }
    public int PdfCount { get; set; }
    public int PrintedCount { get; set; }
    public int JobVersion { get; set; } = 1;
    public int TemplateVersion { get; set; } = 1;

    // Navigation
    public virtual Batch? Batch { get; set; }
}
