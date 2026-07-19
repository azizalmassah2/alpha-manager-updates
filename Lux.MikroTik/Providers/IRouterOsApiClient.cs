using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Providers;

public interface IRouterOsApiClient
{
    bool IsConnected { get; }
    Task ConnectAsync(MikroTikConnectionOptions options);
    Task DisconnectAsync();
    Task<IEnumerable<IDictionary<string, string>>> ExecuteAsync(string command);
    Task<IEnumerable<IDictionary<string, string>>> ExecuteAsync(string command, params string[] parameters);
    Task<string> ExecuteTextAsync(string command);
    Task<string> ExecuteTextAsync(string command, params string[] parameters);
}
