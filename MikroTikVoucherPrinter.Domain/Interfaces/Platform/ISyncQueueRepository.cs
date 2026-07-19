using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace MikroTikVoucherPrinter.Domain.Interfaces.Platform;

public interface ISyncQueueRepository
{
    Task<SyncQueueItem?> GetByIdAsync(Guid id);
    Task<IEnumerable<SyncQueueItem>> GetPendingItemsAsync();
    Task AddAsync(SyncQueueItem item);
    Task UpdateAsync(SyncQueueItem item);
    Task DeleteAsync(Guid id);
}
