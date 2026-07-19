namespace MikroTikVoucherPrinter.Application.DTOs.Platform;

public class DashboardSnapshot
{
    public int TotalProjects { get; set; }
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
    public int OfflineDevices { get; set; }
    public int WarningDevices { get; set; }
    public int CriticalDevices { get; set; }
    public int ActiveMonitoringSessions { get; set; }
}
