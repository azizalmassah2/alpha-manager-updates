using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// خدمة استعلام شاشة المبيعات
/// تقرأ مباشرة من قاعدة بيانات User Manager الأصلية (SQLite)
/// المصدر الرسمي للبيع: userprofile.activated
/// </summary>
public interface ISalesQueryService
{
    /// <summary>
    /// جلب سجلات المبيعات بصفحية Keyset Pagination
    /// يعرض فقط الكروت التي activated > 0
    /// </summary>
    Task<PagedResult<SalesRecordDto>> GetSalesKeysetAsync(
        SalesQueryParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// حساب إحصائيات KPI Cards
    /// </summary>
    Task<SalesKpiDto> GetSalesKpiAsync(
        SalesQueryParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// عدد سجلات المبيعات مع الفلاتر الحالية (للعداد الشامل)
    /// </summary>
    Task<int> GetSalesCountAsync(
        SalesQueryParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// جلب قائمة بأسماء كل الباقات المتوفرة في قاعدة بيانات User Manager
    /// </summary>
    Task<System.Collections.Generic.List<string>> GetProfilesAsync(
        string routerDbPath,
        CancellationToken cancellationToken = default);
}
