using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Providers;

public interface IRouterOsProvider
{
    bool IsConnected { get; }
    Task<Result> ConnectAsync(MikroTikConnectionOptions options);
    Task<Result> DisconnectAsync();
    Task<Result<MikroTikResponse>> ExecuteAsync(MikroTikCommand command);
}
