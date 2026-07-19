using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly IDbContextFactory<LuxCardDbContext> DbFactory;

    public GenericRepository(IDbContextFactory<LuxCardDbContext> dbFactory)
    {
        DbFactory = dbFactory;
    }

    public async Task<T?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<T>().FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<T>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetFilteredAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<T>().Where(predicate).AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<T>().Update(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<T>().Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task HardDeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        var tableName = db.Model.FindEntityType(typeof(T))?.GetTableName() ?? (typeof(T).Name + "s");
        await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM {tableName} WHERE Id = {{0}}", 
            new object[] { entity.Id }, 
            cancellationToken);
    }
}
