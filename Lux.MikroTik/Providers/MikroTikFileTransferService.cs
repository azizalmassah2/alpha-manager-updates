using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.MikroTik.Providers;

public class MikroTikFileTransferService : IFileTransferService
{
    public bool CanHandle(IDevice device) => device.Vendor == DeviceVendor.MikroTik;

    public async Task<Result> UploadAsync(IDevice device, string localPath, string remotePath, CancellationToken cancellationToken = default)
    {
        // Mock FTP upload for now
        await Task.Delay(500, cancellationToken);
        return Result.Success();
    }
}
