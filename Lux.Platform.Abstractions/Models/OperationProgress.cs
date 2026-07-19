namespace Lux.Platform.Abstractions.Models;

public class OperationProgress
{
    public int TotalDevices { get; set; }
    public int ProcessedDevices { get; set; }
    public int SuccessfulDevices { get; set; }
    public int FailedDevices { get; set; }

    public double PercentComplete => TotalDevices == 0 ? 0 : ((double)ProcessedDevices / TotalDevices) * 100;
}
