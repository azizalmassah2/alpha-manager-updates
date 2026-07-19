using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;

namespace Lux.OpenWrt.Interfaces;

public interface IOpenWrtTelemetryProvider
{
    Task<Result<DeviceTelemetry>> GetTelemetryAsync(string deviceId, string ip, string session, CancellationToken cancellationToken = default);
}
