using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IVoucherQueryService
{
    Task<IReadOnlyList<VoucherDto>> GetVouchersByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherDto>> GetPendingSyncVouchersProjectedAsync(CancellationToken cancellationToken = default);
    
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
    [System.Obsolete("Use GetVouchersKeysetAsync instead for paginated loading.", false)]
    Task<IReadOnlyList<VoucherDto>> GetAllVouchersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherDto>> GetAllVouchersFromMikroTikAsync(CancellationToken cancellationToken = default);

    // V2 Paginated and Smart Query signatures
    Task<PagedResult<VoucherDto>> GetVouchersPagedAsync(VoucherQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<PagedResult<VoucherDto>> GetVouchersKeysetAsync(
        VoucherQueryParameters parameters,
        DateTime? afterCreatedAt,
        Guid? afterId,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> GetVoucherCountAsync(VoucherQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>جلسات Hotspot النشطة للمستخدم (إن وُجدت).</summary>
    Task<IReadOnlyList<string>> GetHotspotActiveSessionsForUserAsync(string username, CancellationToken cancellationToken = default);
}
