using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MikroTikVoucherPrinter.Infrastructure.Data;

public class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "Lux Platform");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "platform.db");
        
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new PlatformDbContext(optionsBuilder.Options);
    }
}
