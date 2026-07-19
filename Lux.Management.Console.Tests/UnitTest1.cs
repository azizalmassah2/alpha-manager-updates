using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System;

namespace Lux.Management.Console.Tests;

public class UnitTest1
{
    [Fact]
    public async Task RunTracer()
    {
        var services = new ServiceCollection();
        
        // Add Logging
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add application and infrastructure services
        MikroTikVoucherPrinter.Application.DependencyInjection.AddApplicationServices(services);
        MikroTikVoucherPrinter.Infrastructure.DependencyInjection.AddInfrastructureServices(services);
        Lux.MikroTik.DependencyInjection.AddMikroTikServices(services, useMockProvider: false);

        // Add dispatcher service stub
        var dispatcherMock = new Moq.Mock<Lux.Platform.Abstractions.Interfaces.IDispatcherService>();
        services.AddSingleton(dispatcherMock.Object);

        var sp = services.BuildServiceProvider();

        // Switch to the last active router
        var activeRouterContext = sp.GetRequiredService<IActiveRouterContext>();
        using (var scope = sp.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MikroTikVoucherPrinter.Infrastructure.Data.LuxCardDbContext>>();
            using var luxDb = await dbFactory.CreateDbContextAsync();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<UnitTest1>>();
            await MikroTikVoucherPrinter.Infrastructure.Data.LuxCardSqliteSchemaUpgrade.ApplyAsync(luxDb, logger);

            var db = scope.ServiceProvider.GetRequiredService<MikroTikVoucherPrinter.Infrastructure.Data.PlatformDbContext>();
            var router = await db.Routers.FirstOrDefaultAsync(r => r.Host == "10.0.0.1");
            if (router == null)
            {
                router = await db.Routers.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
            }
            
            if (router != null)
            {
                await activeRouterContext.SwitchRouterAsync(router);
                var queryService = scope.ServiceProvider.GetRequiredService<IVoucherQueryService>();
                var list = await queryService.GetAllVouchersFromMikroTikAsync(CancellationToken.None);
                
                // Write a tag to confirm execution
                var tagPath = @"C:\Users\MrAziz\.gemini\antigravity\brain\6bb8795a-6087-4e0f-984f-b7e2636f66c8\scratch\tracer_success.txt";
                System.IO.File.WriteAllText(tagPath, $"Fetched {list.Count} items at {DateTime.Now}");
            }
        }
    }
}