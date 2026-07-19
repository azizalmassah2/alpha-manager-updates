using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Infrastructure.Data;
using MikroTikVoucherPrinter.Domain.Entities;
using System.Collections.ObjectModel;
using MikroTikVoucherPrinter.Application.DTOs;

class Program
{
    static async Task Main()
    {
        var dbPath = @"C:\Users\MrAziz\AppData\Local\Lux Platform\platform.db";
        var optionsBuilder = new DbContextOptionsBuilder<LuxCardDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        Console.WriteLine("====================================================");
        Console.WriteLine("STARTING FORENSIC PERFORMANCE PROFILING");
        Console.WriteLine("====================================================");

        var sw = new Stopwatch();

        // 1. Database connection and Context Initialization
        sw.Start();
        using var db = new LuxCardDbContext(optionsBuilder.Options, null);
        // Force DB connection open by running a simple query
        var dummy = await db.Database.CanConnectAsync();
        sw.Stop();
        Console.WriteLine($"1. DbContext Initialization & CanConnect: {sw.ElapsedMilliseconds} ms (CanConnect: {dummy})");

        // 2. Fetch the active router
        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>();
        platformOptions.UseSqlite($"Data Source={dbPath}");
        using var platformDb = new PlatformDbContext(platformOptions.Options);
        var router = await platformDb.Routers.FirstOrDefaultAsync();
        if (router == null)
        {
            Console.WriteLine("No router found in database. Exiting.");
            return;
        }
        var routerId = router.Id;
        Console.WriteLine($"Active Router: {router.DisplayName} (ID: {routerId})");

        // 3. Measure Voucher Count query
        sw.Restart();
        var localCount = await db.Vouchers
            .IgnoreQueryFilters()
            .CountAsync(v => v.RouterId == routerId && !v.IsDeleted);
        sw.Stop();
        Console.WriteLine($"2. Count query (Vouchers for active router): {sw.ElapsedMilliseconds} ms (Count: {localCount})");

        // 4. Measure Voucher Full List Load (AsNoTracking)
        sw.Restart();
        var items = await db.Vouchers
            .IgnoreQueryFilters()
            .Include(v => v.Agent)
            .Where(v => v.RouterId == routerId && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .AsNoTracking()
            .Select(v => new VoucherDto
            {
                Id = v.Id,
                Username = v.Username,
                Password = v.Password,
                Profile = v.ProfileName,
                Price = v.Price,
                Status = v.Status,
                IsDisabled = false,
                IsDeleted = v.IsDeleted,
                SyncStatus = v.SyncStatus,
                CreatedAt = v.CreatedAt,
                BatchId = v.BatchId,
                CredentialMode = v.CredentialMode,
                AgentName = v.Agent != null ? v.Agent.Name : "-",
                DownloadUsedBytes = v.DownloadUsedBytes,
                UploadUsedBytes = v.UploadUsedBytes,
                VoucherSource = v.VoucherSource,
                ImportDate = v.ImportDate,
                CreatedBy = v.CreatedBy,
                Comment = v.Comment
            })
            .ToListAsync();
        sw.Stop();
        Console.WriteLine($"3. Full Voucher list EF Core Query (30k rows): {sw.ElapsedMilliseconds} ms (Returned: {items.Count} items)");

        // 5. Measure ObservableCollection Population simulation (analogous to VoucherManagementViewModel.cs lines 749-770)
        Console.WriteLine("\nSimulating UI Thread ObservableCollection update loop...");
        var vouchersCollection = new ObservableCollection<VoucherDto>();
        
        // Simulating the exact logic used in VoucherManagementViewModel
        sw.Restart();
        for (int i = 0; i < items.Count; i++)
        {
            if (i < vouchersCollection.Count)
            {
                if (vouchersCollection[i].Id != items[i].Id || 
                    vouchersCollection[i].Status != items[i].Status ||
                    vouchersCollection[i].SyncStatus != items[i].SyncStatus)
                {
                    vouchersCollection[i] = items[i];
                }
            }
            else
            {
                vouchersCollection.Add(items[i]);
            }
        }
        while (vouchersCollection.Count > items.Count)
        {
            vouchersCollection.RemoveAt(vouchersCollection.Count - 1);
        }
        sw.Stop();
        Console.WriteLine($"4. ObservableCollection population loop: {sw.ElapsedMilliseconds} ms");

        Console.WriteLine("====================================================");
    }
}
