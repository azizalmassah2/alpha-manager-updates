using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lux.Platform.Abstractions.Interfaces;

public interface IReconnectStrategy
{
    Task<bool> WaitForReconnectAsync(IDevice device, TimeSpan timeout, CancellationToken cancellationToken = default);
}
