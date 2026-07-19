using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Connectivity;

public interface IMikroTikConnection : IDisposable, IAsyncDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(MikroTikConnectionOptions options, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
