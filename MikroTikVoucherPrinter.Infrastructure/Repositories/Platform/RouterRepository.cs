using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Repositories.Platform;

public class RouterRepository : IRouterRepository
{
    private readonly PlatformDbContext _context;

    public RouterRepository(PlatformDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Router>> GetAllAsync()
    {
        return await _context.Routers.ToListAsync();
    }

    public async Task<Router?> GetByIdAsync(Guid id)
    {
        return await _context.Routers.FindAsync(id);
    }

    public async Task AddAsync(Router router)
    {
        await _context.Routers.AddAsync(router);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Router router)
    {
        _context.Routers.Update(router);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var router = await _context.Routers.FindAsync(id);
        if (router != null)
        {
            _context.Routers.Remove(router);
            await _context.SaveChangesAsync();
        }
    }
}
