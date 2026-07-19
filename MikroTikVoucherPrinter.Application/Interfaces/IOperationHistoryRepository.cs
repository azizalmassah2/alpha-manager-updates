using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IOperationHistoryRepository
{
    Task SaveAsync(FleetOperation operation, CancellationToken cancellationToken = default);
    Task UpdateAsync(FleetOperation operation, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FleetOperation>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FleetOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
