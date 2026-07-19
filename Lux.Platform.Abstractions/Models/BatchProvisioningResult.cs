using System;
using System.Collections.Generic;
using System.Linq;

namespace Lux.Platform.Abstractions.Models;

public class BatchProvisioningResult
{
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime FinishedAt { get; set; }
    
    public List<DeviceProvisioningResult> DeviceResults { get; set; } = new();

    public int TotalDevices => DeviceResults.Count;
    public int SuccessfulDevices => DeviceResults.Count(r => r.IsSuccess);
    public int FailedDevices => DeviceResults.Count(r => !r.IsSuccess);

    public bool IsFullySuccessful => FailedDevices == 0 && TotalDevices > 0;
}
