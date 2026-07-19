using System;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IVoucherGenerationService
{
    Task<VoucherGenerationResult> GenerateAsync(
        VoucherGenerationRequest request,
        IProgress<(int success, int failed, int total, string phase)>? progress = null,
        CancellationToken cancellationToken = default);
}
