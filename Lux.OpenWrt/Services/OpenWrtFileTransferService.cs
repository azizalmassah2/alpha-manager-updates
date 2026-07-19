using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.OpenWrt.Services;

public class OpenWrtFileTransferService : IFileTransferService
{
    public bool CanHandle(IDevice device) => device.Vendor == DeviceVendor.OpenWrt;

    public async Task<Result> UploadAsync(IDevice device, string localPath, string remotePath, CancellationToken cancellationToken = default)
    {
        // Mock SCP upload for now as required.
        // In a real implementation we would use Renci.SshNet ScpClient.
        await Task.Delay(500, cancellationToken);
        return Result.Success();
    }
}
