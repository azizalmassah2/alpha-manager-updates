using System;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Entities.Operations;

public class OperationJob : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public OperationType OperationType { get; set; }
    public DeviceRole TargetRole { get; set; }
    
    /// <summary>
    /// JSON serialized array of target router IDs.
    /// </summary>
    public string TargetRouterIds { get; set; } = "[]";
    
    public OperationState State { get; set; } = OperationState.Pending;
    public double Progress { get; set; } // 0 to 100
    
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public string? ResultMessage { get; set; }
}
