using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Connectivity;

public interface IMikroTikSessionManager
{
    bool IsConnected { get; }
    Task OpenSessionAsync(MikroTikConnectionOptions options, CancellationToken cancellationToken = default);
    Task CloseSessionAsync(CancellationToken cancellationToken = default);
}
