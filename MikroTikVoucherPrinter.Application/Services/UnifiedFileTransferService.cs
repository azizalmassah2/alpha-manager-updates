using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;

namespace MikroTikVoucherPrinter.Application.Services;

public class UnifiedFileTransferService : IFileTransferService
{
    private readonly IEnumerable<IFileTransferService> _providers;

    public UnifiedFileTransferService(IEnumerable<IFileTransferService> providers)
    {
        _providers = providers;
    }

    public bool CanHandle(IDevice device) => true;

    public Task<Result> UploadAsync(IDevice device, string localPath, string remotePath, CancellationToken cancellationToken = default)
    {
        var provider = _providers.Where(p => p != this).FirstOrDefault(p => p.CanHandle(device));
        if (provider == null)
            return Task.FromResult(Result.Failure($"No file transfer provider found for {device.Vendor}", ErrorType.Unexpected));

        return provider.UploadAsync(device, localPath, remotePath, cancellationToken);
    }
}
