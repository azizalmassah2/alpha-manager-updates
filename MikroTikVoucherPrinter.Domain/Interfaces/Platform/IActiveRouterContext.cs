using System;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Enums.Platform;

namespace MikroTikVoucherPrinter.Domain.Interfaces.Platform;

public interface IActiveRouterContext
{
    Router? CurrentRouter { get; }
    Guid? CurrentRouterId { get; }
    bool IsConnected { get; }
    ConnectionState State { get; }
    
    // Raise event when active router or connection status changes
    event EventHandler? ActiveRouterChanged;

    Task ConnectAsync(Router router, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SwitchRouterAsync(Router router, CancellationToken cancellationToken = default);
}
