using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Repositories.Platform;

public class SyncQueueRepository : ISyncQueueRepository
{
    private readonly PlatformDbContext _context;

    public SyncQueueRepository(PlatformDbContext context)
    {
        _context = context;
    }

    public async Task<SyncQueueItem?> GetByIdAsync(Guid id)
    {
        return await _context.SyncQueue.FindAsync(id);
    }

    public async Task<IEnumerable<SyncQueueItem>> GetPendingItemsAsync()
    {
        return await _context.SyncQueue
            .Where(s => s.Status == "Pending" || s.Status == "Retrying")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(SyncQueueItem item)
    {
        await _context.SyncQueue.AddAsync(item);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SyncQueueItem item)
    {
        _context.SyncQueue.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var item = await _context.SyncQueue.FindAsync(id);
        if (item != null)
        {
            _context.SyncQueue.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}
