using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// محرك تراكم استكشاف واسترجاع حركة مرور الفيلانات والسلسلة الزمانية
/// </summary>
public interface IVlanTelemetryService
{
    /// <summary>
    /// معالجة عينات استهلاك الفيلانات القادمة من المايكروتك وتراكمها تلقائياً مع معالجة إعادة التشغيل وسد الفجوات
    /// </summary>
    Task ProcessVlanSamplesAsync(
        Guid routerId, 
        IEnumerable<(string VlanName, long CurrentRx, long CurrentTx)> vlanSamples, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// جلب الإجمالي التراكمي المحفوظ للفيلانات (شاملاً الحركة السابقة والحالية)
    /// </summary>
    Task<Dictionary<string, (long TotalRx, long TotalTx)>> GetVlanCumulativeTotalsAsync(
        Guid routerId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// توليد تقرير تحليلي مفصل لجميع الفيلانات حسب الفترة الزمنية المحددة
    /// </summary>
    Task<DTOs.VlanAnalyticsReportDto> GetVlanAnalyticsReportAsync(
        Guid routerId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}
