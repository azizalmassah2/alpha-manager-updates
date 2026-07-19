namespace MikroTikVoucherPrinter.Application.DTOs;

public class DashboardStatsDto
{
    public int TotalVouchers { get; set; }
    public int SyncedVouchers { get; set; }
    public int PendingVouchers { get; set; }
    public int FailedVouchers { get; set; }
    public int UsedVouchers { get; set; }
    public int ExpiredVouchers { get; set; }
    public decimal TodaySales { get; set; }
}
