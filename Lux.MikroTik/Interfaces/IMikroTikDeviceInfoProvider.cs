using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Interfaces;

public interface IMikroTikDeviceInfoProvider
{
    Task<Result<MikroTikDeviceInfo>> GetDeviceInfoAsync(IDevice device, CancellationToken cancellationToken = default);
}
