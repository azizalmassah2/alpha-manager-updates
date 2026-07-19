using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class ProgrammingRollbackService : IProgrammingRollbackService
{
    private readonly IUbusClient _ubus;
    private readonly ILogger<ProgrammingRollbackService> _logger;

    public ProgrammingRollbackService(IUbusClient ubus, ILogger<ProgrammingRollbackService> logger)
    {
        _ubus = ubus;
        _logger = logger;
    }

    public async Task RollbackAsync(string ip, string session, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("جاري استرجاع الإعدادات (Rollback) للتغييرات غير الملتزم بها (uncommitted changes) للجهاز {Ip}...", ip);
        
        try
        {
            // Revert changes in uci
            await _ubus.CallAsync(ip, session, "uci", "revert", new { config = "system" }, cancellationToken);
            await _ubus.CallAsync(ip, session, "uci", "revert", new { config = "network" }, cancellationToken);
            await _ubus.CallAsync(ip, session, "uci", "revert", new { config = "wireless" }, cancellationToken);
            await _ubus.CallAsync(ip, session, "dhcp", "revert", new { config = "dhcp" }, cancellationToken);
            
            _logger.LogInformation("تم استرجاع الإعدادات غير الملتزم بها بنجاح.");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "حدث خطأ أثناء إجراء Rollback للجهاز {Ip}: {Message}", ip, ex.Message);
        }
    }
}
