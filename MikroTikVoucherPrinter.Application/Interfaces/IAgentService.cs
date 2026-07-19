using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Entities;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IAgentService
{
    Task<IReadOnlyList<AgentDto>> GetAllAgentsAsync(CancellationToken cancellationToken = default);
    Task<AgentDto?> GetAgentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AgentDto> CreateAgentAsync(Agent agent, CancellationToken cancellationToken = default);
    Task UpdateAgentAsync(Agent agent, CancellationToken cancellationToken = default);
    Task DeleteAgentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ToggleActiveAsync(Guid id, CancellationToken cancellationToken = default);
}
