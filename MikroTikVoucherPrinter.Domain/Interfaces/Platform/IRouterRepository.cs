using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace MikroTikVoucherPrinter.Domain.Interfaces.Platform;

public interface IRouterRepository
{
    Task<IEnumerable<Router>> GetAllAsync();
    Task<Router?> GetByIdAsync(Guid id);
    Task AddAsync(Router router);
    Task UpdateAsync(Router router);
    Task DeleteAsync(Guid id);
}
