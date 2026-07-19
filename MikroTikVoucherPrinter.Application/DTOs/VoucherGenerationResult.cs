using System;

namespace MikroTikVoucherPrinter.Application.DTOs;

public class VoucherGenerationResult
{
    public Guid BatchId { get; set; }
    public int GeneratedCount { get; set; }
    public int DbSuccessCount { get; set; }
    public int DbFailedCount { get; set; }
    public int SyncSuccessCount { get; set; }
    public int SyncFailedCount { get; set; }
    public bool AutoPrintInvoked { get; set; }
    public bool IsSuccess => DbSuccessCount > 0;
}
