using System;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace MikroTikVoucherPrinter.Domain.Entities.Telemetry;

public class AlertCandidate : BaseEntity
{
    public Guid RouterId { get; set; }
    public Router Router { get; set; } = null!;
    
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
