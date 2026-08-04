using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public partial class VlanReportViewModel : ObservableObject
{
    private readonly IVlanTelemetryService _telemetryService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly ILogger<VlanReportViewModel> _logger;

    [ObservableProperty]
    private int _selectedPeriodIndex = 0; // 0 = Today, 1 = This Week, 2 = This Month, 3 = Custom

    [ObservableProperty]
    private DateTime _fromDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _toDate = DateTime.Today.AddDays(1).AddTicks(-1);

    [ObservableProperty]
    private VlanAnalyticsReportDto? _report;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<VlanReportDto> VlanItems { get; } = new();

    public VlanReportViewModel(
        IVlanTelemetryService telemetryService,
        IActiveRouterContext activeRouterContext,
        ILogger<VlanReportViewModel> logger)
    {
        _telemetryService = telemetryService;
        _activeRouterContext = activeRouterContext;
        _logger = logger;
    }

    partial void OnSelectedPeriodIndexChanged(int value)
    {
        var now = DateTime.Now;
        switch (value)
        {
            case 0: // اليوم
                FromDate = now.Date;
                ToDate = now.Date.AddDays(1).AddTicks(-1);
                break;
            case 1: // هذا الأسبوع
                int diff = (7 + (now.DayOfWeek - DayOfWeek.Saturday)) % 7;
                FromDate = now.Date.AddDays(-diff);
                ToDate = now.Date.AddDays(1).AddTicks(-1);
                break;
            case 2: // هذا الشهر
                FromDate = new DateTime(now.Year, now.Month, 1);
                ToDate = FromDate.AddMonths(1).AddTicks(-1);
                break;
            case 3: // مخصص
                break;
        }

        _ = LoadReportAsync();
    }

    [RelayCommand]
    public async Task LoadReportAsync()
    {
        var routerId = _activeRouterContext.CurrentRouter?.Id ?? Guid.Empty;
        if (routerId == Guid.Empty) return;

        IsLoading = true;
        try
        {
            Report = await _telemetryService.GetVlanAnalyticsReportAsync(routerId, FromDate, ToDate);

            VlanItems.Clear();
            if (Report?.VlanItems != null)
            {
                foreach (var item in Report.VlanItems)
                {
                    VlanItems.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء تحميل تقرير تحليلات الفيلانات");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (VlanItems.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Filter = "ملفات CSV (*.csv)|*.csv",
            FileName = $"تقرير_الفيلانات_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("الترتيب,اسم الفيلان,إجمالي التحميل,إجمالي الرفع,الإجمالي الكلي,نسبة الاستهلاك %");
                foreach (var item in VlanItems)
                {
                    sb.AppendLine($"{item.Rank},{item.VlanName},{item.FormattedDownload},{item.FormattedUpload},{item.FormattedTotal},{item.FormattedSharePercent}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("تم تصدير التقرير بنجاح!", "تصدير التقرير", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر تصدير الملف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
