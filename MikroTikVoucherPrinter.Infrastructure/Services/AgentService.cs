using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class AgentService : IAgentService
{
    private readonly IDbContextFactory<LuxCardDbContext> _factory;
    private readonly ILogger<AgentService> _logger;

    public AgentService(IDbContextFactory<LuxCardDbContext> factory, ILogger<AgentService> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    public async Task<IReadOnlyList<AgentDto>> GetAllAgentsAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        var agents = await ctx.Agents
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AgentDto
            {
                Id             = a.Id,
                Name           = a.Name,
                Phone          = a.Phone,
                Notes          = a.Notes,
                CommissionRate = a.CommissionRate,
                Balance        = a.Balance,
                IsActive       = a.IsActive,
                VoucherCount   = ctx.Vouchers.Count(v => v.AgentId == a.Id),
                CreatedAt      = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("✅ تم جلب {Count} وكيل", agents.Count);
        return agents;
    }

    public async Task<AgentDto?> GetAgentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        return await ctx.Agents
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AgentDto
            {
                Id             = a.Id,
                Name           = a.Name,
                Phone          = a.Phone,
                Notes          = a.Notes,
                CommissionRate = a.CommissionRate,
                Balance        = a.Balance,
                IsActive       = a.IsActive,
                VoucherCount   = ctx.Vouchers.Count(v => v.AgentId == a.Id),
                CreatedAt      = a.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AgentDto> CreateAgentAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        ctx.Agents.Add(agent);
        await ctx.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("✅ تم إنشاء الوكيل: {Name}", agent.Name);

        return new AgentDto
        {
            Id             = agent.Id,
            Name           = agent.Name,
            Phone          = agent.Phone,
            Notes          = agent.Notes,
            CommissionRate = agent.CommissionRate,
            Balance        = agent.Balance,
            IsActive       = agent.IsActive,
            VoucherCount   = 0,
            CreatedAt      = agent.CreatedAt
        };
    }

    public async Task UpdateAgentAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await ctx.Agents.FindAsync(new object[] { agent.Id }, cancellationToken);
        if (existing == null)
            throw new InvalidOperationException($"الوكيل بالمعرّف {agent.Id} غير موجود.");

        existing.Name           = agent.Name;
        existing.Phone          = agent.Phone;
        existing.Notes          = agent.Notes;
        existing.CommissionRate = agent.CommissionRate;
        existing.Balance        = agent.Balance;
        existing.IsActive       = agent.IsActive;

        await ctx.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("✅ تم تحديث بيانات الوكيل: {Name}", agent.Name);
    }

    public async Task DeleteAgentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        var agent = await ctx.Agents.FindAsync(new object[] { id }, cancellationToken);
        if (agent == null) return;

        ctx.Agents.Remove(agent);
        await ctx.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("🗑️ تم حذف الوكيل: {Id}", id);
    }

    public async Task<bool> ToggleActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        var agent = await ctx.Agents.FindAsync(new object[] { id }, cancellationToken);
        if (agent == null) return false;

        agent.IsActive = !agent.IsActive;
        await ctx.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("🔄 تم تبديل حالة الوكيل {Id} إلى {State}", id, agent.IsActive);
        return agent.IsActive;
    }
}
