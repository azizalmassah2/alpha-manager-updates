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

using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class AgentService : IAgentService
{
    private readonly IDbContextFactory<LuxCardDbContext> _factory;
    private readonly IActiveRouterContext _routerContext;
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IDbContextFactory<LuxCardDbContext> factory,
        IActiveRouterContext routerContext,
        ILogger<AgentService> logger)
    {
        _factory = factory;
        _routerContext = routerContext;
        _logger  = logger;
    }

    public async Task<IReadOnlyList<AgentDto>> GetAllAgentsAsync(CancellationToken cancellationToken = default)
    {
        var routerId = _routerContext.CurrentRouterId;
        if (routerId == null || routerId == Guid.Empty)
        {
            return Array.Empty<AgentDto>();
        }

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        var agentEntities = await ctx.Agents
            .AsNoTracking()
            .Where(a => a.RouterId == routerId.Value)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var list = new List<AgentDto>();
        foreach (var a in agentEntities)
        {
            var vouchers = await ctx.Vouchers
                .AsNoTracking()
                .Where(v => v.AgentId == a.Id && v.RouterId == routerId.Value)
                .ToListAsync(cancellationToken);

            var voucherCount = vouchers.Count;
            var sales = vouchers.Sum(v => v.Price);
            var commission = sales * (a.CommissionRate / 100m);
            var netOwed = (sales - commission) - a.Balance;

            list.Add(new AgentDto
            {
                Id             = a.Id,
                Name           = a.Name,
                Phone          = a.Phone,
                Notes          = a.Notes,
                CommissionRate = a.CommissionRate,
                Balance        = a.Balance,
                IsActive       = a.IsActive,
                VoucherCount   = voucherCount,
                TotalSalesAmount = sales,
                EarnedCommission = commission,
                NetOwedBalance  = netOwed,
                CreatedAt      = a.CreatedAt
            });
        }

        _logger.LogInformation("✅ تم جلب {Count} وكيل مع تفاصيل المبيعات والعمولات للراوتر {RouterId}", list.Count, routerId);
        return list;
    }

    public async Task<AgentDto?> GetAgentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        var a = await ctx.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (a == null) return null;

        var vouchers = await ctx.Vouchers.AsNoTracking().Where(v => v.AgentId == a.Id).ToListAsync(cancellationToken);
        var sales = vouchers.Sum(v => v.Price);
        var commission = sales * (a.CommissionRate / 100m);
        var netOwed = (sales - commission) - a.Balance;

        return new AgentDto
        {
            Id             = a.Id,
            Name           = a.Name,
            Phone          = a.Phone,
            Notes          = a.Notes,
            CommissionRate = a.CommissionRate,
            Balance        = a.Balance,
            IsActive       = a.IsActive,
            VoucherCount   = vouchers.Count,
            TotalSalesAmount = sales,
            EarnedCommission = commission,
            NetOwedBalance  = netOwed,
            CreatedAt      = a.CreatedAt
        };
    }

    public async Task<AgentDto> CreateAgentAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        if (agent.RouterId == Guid.Empty && _routerContext.CurrentRouterId.HasValue)
        {
            agent.RouterId = _routerContext.CurrentRouterId.Value;
        }

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        ctx.Agents.Add(agent);
        await ctx.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("✅ تم إنشاء الوكيل: {Name} للراوتر {RouterId}", agent.Name, agent.RouterId);

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
            TotalSalesAmount = 0,
            EarnedCommission = 0,
            NetOwedBalance  = 0,
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

    public async Task SettleAgentBalanceAsync(Guid id, decimal amount, string? notes, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        var agent = await ctx.Agents.FindAsync(new object[] { id }, cancellationToken);
        if (agent == null) throw new InvalidOperationException($"الوكيل بالمعرف {id} غير موجود.");

        agent.Balance += amount;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            agent.Notes = string.IsNullOrWhiteSpace(agent.Notes)
                ? $"[{DateTime.Now:yyyy-MM-dd}] تسديد: {amount:N0} - {notes}"
                : $"{agent.Notes}\n[{DateTime.Now:yyyy-MM-dd}] تسديد: {amount:N0} - {notes}";
        }

        await ctx.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("✅ تم تسديد مبلغ {Amount} لحساب الوكيل {Name}", amount, agent.Name);
    }
}
