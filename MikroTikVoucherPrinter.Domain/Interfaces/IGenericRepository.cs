using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MikroTikVoucherPrinter.Domain.Common;

namespace MikroTikVoucherPrinter.Domain.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetFilteredAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task HardDeleteAsync(T entity, CancellationToken cancellationToken = default);
}
