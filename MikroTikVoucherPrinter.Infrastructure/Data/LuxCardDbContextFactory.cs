using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Domain.Enums.Platform;

namespace MikroTikVoucherPrinter.Infrastructure.Data;

public class LuxCardDbContextFactory : IDesignTimeDbContextFactory<LuxCardDbContext>
{
    public LuxCardDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LuxCardDbContext>();
        optionsBuilder.UseSqlite("Data Source=LuxCard.db");

        return new LuxCardDbContext(optionsBuilder.Options, new DummyActiveRouterContext());
    }

    private class DummyActiveRouterContext : IActiveRouterContext
    {
        public Router? CurrentRouter => null;
        public Guid? CurrentRouterId => Guid.Empty;
        public bool IsConnected => false;
        public ConnectionState State => ConnectionState.Disconnected;
        public event EventHandler? ActiveRouterChanged;

        public Task ConnectAsync(Router router, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SwitchRouterAsync(Router router, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void MarkDisconnected(string reason) { }
    }
}
