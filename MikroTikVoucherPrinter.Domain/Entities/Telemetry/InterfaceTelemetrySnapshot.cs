using System;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace MikroTikVoucherPrinter.Domain.Entities.Telemetry;

public class InterfaceTelemetrySnapshot : BaseEntity
{
    public Guid RouterId { get; set; }
    public Router Router { get; set; } = null!;
    
    public string InterfaceName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
    public long RxPackets { get; set; }
    public long TxPackets { get; set; }
    
    public bool Running { get; set; }
}
