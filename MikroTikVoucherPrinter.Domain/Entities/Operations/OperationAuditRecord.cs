using System;
using MikroTikVoucherPrinter.Domain.Common;

namespace MikroTikVoucherPrinter.Domain.Entities.Operations;

public class OperationAuditRecord : BaseEntity
{
    public Guid JobId { get; set; }
    
    /// <summary>
    /// Specific router this audit record is for (can be null if it's a global operation).
    /// </summary>
    public Guid? RouterId { get; set; }
    
    public string UserId { get; set; } = "System"; // e.g. the user who triggered it
    
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    public string Status { get; set; } = string.Empty; // Success, Failed, Cancelled
    public string? FailureReason { get; set; }
}
