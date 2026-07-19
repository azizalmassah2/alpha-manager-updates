using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class NetworkConfigurationService : INetworkConfigurationService
{
    private readonly IUciService _uci;
    private readonly ILogger<NetworkConfigurationService> _logger;

    public NetworkConfigurationService(IUciService uci, ILogger<NetworkConfigurationService> logger)
    {
        _uci = uci;
        _logger = logger;
    }

    public async Task SetLanIpAsync(string ip, string session, string lanSection, string ipaddr, string gateway, string netmask, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("جاري ضبط إعدادات IP للقسم {LanSection}: العنوان={Ipaddr}، البوابة={Gateway}، القناع={Netmask}...", lanSection, ipaddr, gateway, netmask);
        
        var values = new Dictionary<string, object>
        {
            { "ipaddr", ipaddr },
            { "netmask", netmask }
        };

        if (!string.IsNullOrWhiteSpace(gateway))
        {
            values["gateway"] = gateway;
        }
        else
        {
            try
            {
                await _uci.DeleteAsync(ip, session, "network", lanSection, "gateway", cancellationToken);
            }
            catch { /* Ignore if it didn't exist */ }
        }

        await _uci.SetAsync(ip, session, "network", lanSection, values, cancellationToken);
        _logger.LogInformation("تم ضبط إعدادات IP لقسم {LanSection} بنجاح.", lanSection);
    }

    public async Task DisableDhcpAsync(string ip, string session, string lanSection, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("جاري تعطيل خادم DHCP وجهاز IPv6 RA لقسم LAN ({LanSection})...", lanSection);

        try
        {
            await _uci.SetOptionAsync(ip, session, "dhcp", lanSection, "ignore", 1, cancellationToken);

            try { await _uci.DeleteAsync(ip, session, "dhcp", lanSection, "ra", cancellationToken); } catch { }
            try { await _uci.DeleteAsync(ip, session, "dhcp", lanSection, "ra_flags", cancellationToken); } catch { }
            try { await _uci.DeleteAsync(ip, session, "dhcp", lanSection, "dhcpv6", cancellationToken); } catch { }

            _logger.LogInformation("تم تعطيل خادم DHCP و IPv6 RA/dhcpv6 بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("حدث خطأ أثناء تعطيل DHCP (قد لا يحتوي الجهاز على قسم DHCP): {Message}", ex.Message);
        }
    }
}
