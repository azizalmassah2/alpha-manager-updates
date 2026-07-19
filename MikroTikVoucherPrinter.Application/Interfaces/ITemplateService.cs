using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface ITemplateService
{
    Task<IReadOnlyList<TemplateConfigDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<TemplateConfigDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TemplateConfigDto?> GetDefaultForProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TemplateConfigDto>> GetByKindAsync(TemplateType kind, CancellationToken cancellationToken = default);

    /// <summary>ظ…ط¹ط±ظ‘ظپ ط§ظ„ظ‚ط§ظ„ط¨ ط§ظ„ظ†ط¸ط§ظ…ظٹ ط§ظ„ط§ظپطھط±ط§ط¶ظٹ (A4 ISP) ط¨ط¹ط¯ ط§ظ„طھط£ظƒط¯ ظ…ظ† ظˆط¬ظˆط¯ظ‡ ظپظٹ ط§ظ„ظ‚ط§ط¹ط¯ط©.</summary>
    Task<Guid> GetPrimarySystemTemplateIdAsync(CancellationToken cancellationToken = default);
}
