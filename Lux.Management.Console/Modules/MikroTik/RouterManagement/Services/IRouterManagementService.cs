using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

public interface IRouterManagementService
{
    Task<MikroTikResponse> ExecuteQueryAsync(string commandText, CancellationToken cancellationToken = default);
    Task<MikroTikResponse> ExecuteCommandAsync(string commandText, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default);
}
