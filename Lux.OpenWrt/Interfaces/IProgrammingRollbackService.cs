using System.Threading;
using System.Threading.Tasks;

namespace Lux.OpenWrt.Interfaces;

public interface IProgrammingRollbackService
{
    Task RollbackAsync(string ip, string session, CancellationToken cancellationToken = default);
}
