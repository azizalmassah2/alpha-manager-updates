using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IRouterCapabilityService
{
    Task<string> GetProfileSystemTypeAsync(CancellationToken cancellationToken = default);
    void ClearCache();
}
