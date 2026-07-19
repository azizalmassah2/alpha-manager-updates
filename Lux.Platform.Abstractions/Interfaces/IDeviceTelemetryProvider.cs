using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;

namespace Lux.Platform.Abstractions.Interfaces;

/// <summary>
/// العقد الموحد لمزودي بيانات المراقبة للأجهزة المختلفة
/// </summary>
public interface IDeviceTelemetryProvider
{
    Task<Result<DeviceTelemetry>> GetTelemetryAsync(IDevice device, string session, CancellationToken cancellationToken = default);
}
