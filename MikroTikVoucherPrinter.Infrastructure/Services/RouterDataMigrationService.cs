using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class RouterDataMigrationService : IRouterDataMigrationService
{
    private readonly PlatformDbContext _platformDbContext;
    private readonly LuxCardDbContext _luxCardDbContext;
    private readonly ILogger<RouterDataMigrationService> _logger;

    public RouterDataMigrationService(
        PlatformDbContext platformDbContext,
        LuxCardDbContext luxCardDbContext,
        ILogger<RouterDataMigrationService> logger)
    {
        _platformDbContext = platformDbContext;
        _luxCardDbContext = luxCardDbContext;
        _logger = logger;
    }

    public async Task MigrateNullRouterIdsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting RouterDataMigrationService...");

        // Ensure we have a router available
        var defaultRouter = await _platformDbContext.Routers.FirstOrDefaultAsync(cancellationToken);
        if (defaultRouter == null)
        {
            _logger.LogWarning("No router found in PlatformDb. Cannot migrate NULL RouterIds.");
            return;
        }

        var routerId = defaultRouter.Id.ToString();
        var tables = new[] { "Vouchers", "Batches", "Profiles", "Agents", "TemplateConfigs" };

        foreach (var table in tables)
        {
            try
            {
                var sql = $"UPDATE {table} SET RouterId = '{routerId}' WHERE RouterId IS NULL OR RouterId = '00000000-0000-0000-0000-000000000000'";
                var affectedRows = await _luxCardDbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                if (affectedRows > 0)
                {
                    _logger.LogInformation("Migrated {Count} records in table {Table} to RouterId {RouterId}", affectedRows, table, routerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate RouterId for table {Table}", table);
            }
        }
        
        _logger.LogInformation("RouterDataMigrationService finished.");
    }

    public async Task MigrateNullSystemTypesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Migration of Null SystemTypes in Profiles...");

        try
        {
            var sql = "UPDATE Profiles SET SystemType = 'Hotspot' WHERE SystemType IS NULL";
            var affectedRows = await _luxCardDbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            if (affectedRows > 0)
            {
                _logger.LogInformation("Migrated {Count} legacy Profiles by setting SystemType to 'Hotspot'.", affectedRows);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate SystemType for Profiles table.");
        }

        _logger.LogInformation("SystemType Migration finished.");
    }
}
