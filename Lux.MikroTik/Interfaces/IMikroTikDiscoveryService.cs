using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.MikroTik.Interfaces;

public interface IMikroTikDiscoveryService
{
    Task<Result<NetworkDevice>> DiscoverAsync(IDevice device, CancellationToken cancellationToken = default);
}
