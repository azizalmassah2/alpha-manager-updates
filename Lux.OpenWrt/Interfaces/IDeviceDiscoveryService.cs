using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.OpenWrt.Interfaces;

public interface IDeviceDiscoveryService
{
    Task<Result<NetworkDevice>> DiscoverDeviceAsync(string ip, string session, CancellationToken cancellationToken = default);
}
