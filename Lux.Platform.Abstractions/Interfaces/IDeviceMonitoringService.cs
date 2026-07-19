using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace Lux.Platform.Abstractions.Interfaces;

public interface IDeviceMonitoringService
{
    Task<Result<DeviceTelemetry>> GetTelemetryAsync(string deviceId, string host, string username, string password, CancellationToken cancellationToken = default);
}
