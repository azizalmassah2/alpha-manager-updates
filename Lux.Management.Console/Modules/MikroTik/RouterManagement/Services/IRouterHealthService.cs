using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

public enum RouterHealthLevel
{
    Unknown,
    Healthy,
    Warning,
    Critical
}

public class RouterHealthStatus
{
    public RouterHealthLevel OverallHealth { get; set; } = RouterHealthLevel.Unknown;
    public double CpuLoadPercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public string Uptime { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
    public double? Temperature { get; set; }
    public double? Voltage { get; set; }
    public bool IsConnected { get; set; }
    public DateTime LastUpdated { get; set; }
}

public interface IRouterHealthService
{
    RouterHealthStatus CurrentStatus { get; }
    event EventHandler<RouterHealthStatus>? HealthUpdated;
    
    // Polling control
    TimeSpan PollingInterval { get; set; }
}
