using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Connectivity;

public class MikroTikSessionManager : IMikroTikSessionManager
{
    private readonly IMikroTikConnection _connection;

    public MikroTikSessionManager(IMikroTikConnection connection)
    {
        _connection = connection;
    }

    public bool IsConnected => _connection.IsConnected;

    public async Task OpenSessionAsync(MikroTikConnectionOptions options, CancellationToken cancellationToken = default)
    {
        if (_connection.IsConnected)
        {
            try { await _connection.DisconnectAsync(cancellationToken); } catch { /* ignore */ }
        }
        await _connection.ConnectAsync(options, cancellationToken);
    }

    public async Task CloseSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection.IsConnected)
        {
            await _connection.DisconnectAsync(cancellationToken);
        }
    }
}
