using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

public class RouterManagementService : IRouterManagementService
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RouterManagementService> _logger;

    public RouterManagementService(
        IActiveRouterContext activeRouterContext,
        IServiceScopeFactory scopeFactory,
        ILogger<RouterManagementService> logger)
    {
        _activeRouterContext = activeRouterContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private async Task<MikroTikResponse> ExecuteInternalAsync(MikroTikCommand command, CancellationToken cancellationToken)
    {
        if (!_activeRouterContext.IsConnected || _activeRouterContext.CurrentRouter == null)
        {
            throw new InvalidOperationException("No active router is currently connected.");
        }

        using var scope = _scopeFactory.CreateScope();
        var commandExecutor = scope.ServiceProvider.GetRequiredService<IMikroTikCommandExecutor>();

        return await commandExecutor.ExecuteAsync(command, cancellationToken);
    }

    public async Task<MikroTikResponse> ExecuteQueryAsync(string commandText, CancellationToken cancellationToken = default)
    {
        var command = new MikroTikCommand { Command = commandText };
        return await ExecuteInternalAsync(command, cancellationToken);
    }

    public async Task<MikroTikResponse> ExecuteCommandAsync(string commandText, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default)
    {
        var command = new MikroTikCommand { Command = commandText };
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                command.Parameters[kvp.Key] = kvp.Value;
            }
        }
        return await ExecuteInternalAsync(command, cancellationToken);
    }
}
