using System;
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
    /// توليد ملف PDF يحتوي على الكروت بتنسيق A4 أو حراري مع إمكانية التقرير المرحلي لتقدم البناء.
    /// </summary>
    Task<Result<byte[]>> GeneratePdfAsync(
        List<VoucherDto> vouchers,
        PrintSettingsDto settings,
        IProgress<(int currentPage, int totalPages, string statusText)>? progress = null,
        CancellationToken cancellationToken = default);
}
