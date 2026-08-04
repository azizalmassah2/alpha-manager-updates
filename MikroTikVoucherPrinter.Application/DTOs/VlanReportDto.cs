using System;

namespace MikroTikVoucherPrinter.Application.DTOs;

public class VlanReportDto
{
    public int Rank { get; set; }
    public string VlanName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SubnetOrIp { get; set; } = string.Empty;
    
    public long DownloadBytes { get; set; }
    public long UploadBytes { get; set; }
    public long TotalBytes => DownloadBytes + UploadBytes;

    public string FormattedDownload => FormatBytes(DownloadBytes);
    public string FormattedUpload => FormatBytes(UploadBytes);
    public string FormattedTotal => FormatBytes(TotalBytes);

    public double NetworkSharePercent { get; set; }
    public string FormattedSharePercent => $"{NetworkSharePercent:F1}%";

    public int PeakConnectedClients { get; set; }
    public string HealthStatus { get; set; } = "🟢 نشط";
    public DateTime LastActiveTime { get; set; } = DateTime.Now;

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        double tb = 1099511627776.0;
        double gb = 1073741824.0;
        double mb = 1048576.0;
        double kb = 1024.0;

        if (bytes >= tb) return $"{bytes / tb:F2} TB";
        if (bytes >= gb) return $"{bytes / gb:F2} GB";
        if (bytes >= mb) return $"{bytes / mb:F2} MB";
        if (bytes >= kb) return $"{bytes / kb:F2} KB";
        return $"{bytes} B";
    }
}

public class VlanAnalyticsReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    
    public string PeriodTitle { get; set; } = string.Empty;
    public long GrandTotalBytes { get; set; }
    public string FormattedGrandTotal => VlanReportDto.FormatBytes(GrandTotalBytes);

    public VlanReportDto? TopUsageVlan { get; set; }
    public VlanReportDto? LeastUsageVlan { get; set; }
    public VlanReportDto? PeakClientsVlan { get; set; }

    public System.Collections.Generic.List<VlanReportDto> VlanItems { get; set; } = new();
}
