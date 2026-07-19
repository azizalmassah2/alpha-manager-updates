using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IRouterDataMigrationService
{
    Task MigrateNullRouterIdsAsync(CancellationToken cancellationToken = default);
    Task MigrateNullSystemTypesAsync(CancellationToken cancellationToken = default);
}
