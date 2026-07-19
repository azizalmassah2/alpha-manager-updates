using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IPrintService
{
    /// <summary>
    /// طھظˆظ„ظٹط¯ ظ…ظ„ظپ PDF ظٹط­طھظˆظٹ ط¹ظ„ظ‰ ط§ظ„ظƒط±ظˆطھ ط¨طھظ†ط³ظٹظ‚ A4 ط£ظˆ ط­ط±ط§ط±ظٹ ط¨ط´ظƒظ„ ظ…طھط¯ظپظ‚
    /// </summary>
    Task<Result<byte[]>> GeneratePdfAsync(List<VoucherDto> vouchers, PrintSettingsDto settings, CancellationToken cancellationToken = default);
}
