using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class HostnameConfigurationService : IHostnameConfigurationService
{
    private readonly IUciService _uci;
    private readonly ILogger<HostnameConfigurationService> _logger;

    public HostnameConfigurationService(IUciService uci, ILogger<HostnameConfigurationService> logger)
    {
        _uci = uci;
        _logger = logger;
    }

    public async Task ConfigureHostnameAsync(string ip, string session, string targetIp, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("جاري ضبط اسم المضيف (Hostname)...");
        var hostname = GenerateHostname(targetIp);
        
        var systemConfig = await _uci.GetConfigDictAsync(ip, session, "system", cancellationToken);
        var systemSection = "@system[0]";
        
        foreach (var key in systemConfig.Keys)
        {
            if (systemConfig[key] is Dictionary<string, object> sDict && sDict.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "system")
            {
                systemSection = key;
                break;
            }
        }
        
        await _uci.SetOptionAsync(ip, session, "system", systemSection, "hostname", hostname, cancellationToken);
        _logger.LogInformation("تم تعيين اسم المضيف الجديد: {Hostname}", hostname);
    }

    private string GenerateHostname(string targetIp)
    {
        return $"Lux-{targetIp.Replace(".", "-")}";
    }
}
