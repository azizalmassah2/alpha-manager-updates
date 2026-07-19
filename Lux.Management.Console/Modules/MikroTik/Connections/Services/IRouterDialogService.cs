using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Services;

public interface IRouterDialogService
{
    Task<Router?> ShowAddEditRouterDialogAsync(Router? existingRouter = null);
}
