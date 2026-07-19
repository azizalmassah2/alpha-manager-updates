using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Application.Services;

public class InMemoryOperationHistoryRepository : IOperationHistoryRepository
{
    private readonly ConcurrentDictionary<Guid, FleetOperation> _store = new();

    public Task SaveAsync(FleetOperation operation, CancellationToken cancellationToken = default)
    {
        _store[operation.Id] = operation;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FleetOperation operation, CancellationToken cancellationToken = default)
    {
        _store[operation.Id] = operation;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<FleetOperation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<FleetOperation>>(_store.Values.ToList());
    }

    public Task<FleetOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var operation);
        return Task.FromResult(operation);
    }
}
