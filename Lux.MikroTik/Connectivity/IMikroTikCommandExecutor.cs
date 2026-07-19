using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Connectivity;

public interface IMikroTikCommandExecutor
{
    Task<MikroTikResponse> ExecuteAsync(MikroTikCommand command, CancellationToken cancellationToken = default);
}
