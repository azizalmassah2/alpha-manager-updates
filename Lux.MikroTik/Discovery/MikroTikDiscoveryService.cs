using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Interfaces;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.MikroTik.Discovery;

public class MikroTikDiscoveryService : IMikroTikDiscoveryService
{
    private readonly IMikroTikDeviceInfoProvider _infoProvider;

    public MikroTikDiscoveryService(IMikroTikDeviceInfoProvider infoProvider)
    {
        _infoProvider = infoProvider;
    }

    public async Task<Result<NetworkDevice>> DiscoverAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        var result = await _infoProvider.GetDeviceInfoAsync(device, cancellationToken);
        
        if (result.IsFailure)
        {
            return Result<NetworkDevice>.Failure(result.ErrorMessage, result.ErrorType);
        }

        var info = result.Value;

        var networkDevice = new NetworkDevice
        {
            Vendor = DeviceVendor.MikroTik,
            Status = DeviceStatus.Online,
            Name = info.Identity,
            FirmwareVersion = info.FirmwareVersion,
            Model = info.Model,
            IpAddress = device.IpAddress, // Inherit IP
            MacAddress = string.Empty // Placeholder
        };

        return Result<NetworkDevice>.Success(networkDevice);
    }
}
