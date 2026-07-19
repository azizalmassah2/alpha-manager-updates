using System;
using System.Collections.Generic;

namespace Lux.Platform.Abstractions.Models;

public class FleetOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public FleetOperationType Type { get; set; }
    public FleetOperationStatus Status { get; set; } = FleetOperationStatus.Pending;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public OperationProgress Progress { get; set; } = new();
    public List<DeviceOperationResult> DeviceResults { get; set; } = new();
}
