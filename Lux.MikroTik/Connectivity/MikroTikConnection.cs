using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;

using Lux.MikroTik.Providers;
using Lux.MikroTik.Exceptions;

namespace Lux.MikroTik.Connectivity;

public class MikroTikConnection : IMikroTikConnection
{
    private readonly IRouterOsProvider _provider;

    public MikroTikConnection(IRouterOsProvider provider)
    {
        _provider = provider;
    }

    public bool IsConnected => _provider.IsConnected;

    public async Task ConnectAsync(MikroTikConnectionOptions options, CancellationToken cancellationToken = default)
    {
        var result = await _provider.ConnectAsync(options);
        if (result.IsFailure)
        {
            throw new MikroTikConnectionException($"Failed to connect to MikroTik device: {result.ErrorMessage}");
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var result = await _provider.DisconnectAsync();
        if (result.IsFailure)
        {
            throw new MikroTikConnectionException($"Failed to disconnect: {result.ErrorMessage}");
        }
    }

    public void Dispose()
    {
        // Dispose resources if any
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
