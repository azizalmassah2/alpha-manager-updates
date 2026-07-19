using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class WirelessConfigurationService : IWirelessConfigurationService
{
    private readonly IUciService _uci;
    private readonly ILogger<WirelessConfigurationService> _logger;

    public WirelessConfigurationService(IUciService uci, ILogger<WirelessConfigurationService> logger)
    {
        _uci = uci;
        _logger = logger;
    }

    public async Task ConfigureRadioApAsync(string ip, string session, string radioName, string ifaceSection, string ssid, string password, string networkName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("جاري إعداد الواي فاي للراديو {RadioName} (وضع AP) باسم الشبكة: {Ssid} والشبكة {NetworkName}...", radioName, ssid, networkName);

        var section = ifaceSection;
        if (string.IsNullOrEmpty(section))
        {
            section = await _uci.AddSectionAsync(ip, session, "wireless", "wifi-iface", null, cancellationToken);
        }

        var values = new Dictionary<string, object>
        {
            { "device", radioName },
            { "mode", "ap" },
            { "ssid", ssid },
            { "network", networkName }
        };

        if (!string.IsNullOrWhiteSpace(password))
        {
            values["encryption"] = "psk2";
            values["key"] = password;
        }
        else
        {
            values["encryption"] = "none";
            try { await _uci.DeleteAsync(ip, session, "wireless", section, "key", cancellationToken); } catch { }
        }

        try { await _uci.SetOptionAsync(ip, session, "wireless", radioName, "disabled", 0, cancellationToken); } catch { }

        await _uci.SetAsync(ip, session, "wireless", section, values, cancellationToken);
        _logger.LogInformation("تم إعداد الراديو {RadioName} بنجاح.", radioName);
    }

    public async Task ConfigureRadioStaWdsAsync(string ip, string session, string radioName, string ifaceSection, string remoteSsid, string remotePassword, string networkName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("جاري إعداد الواي فاي للراديو {RadioName} (وضع Client WDS) للاتصال بالشبكة البعيدة: {RemoteSsid}...", radioName, remoteSsid);

        var section = ifaceSection;
        if (string.IsNullOrEmpty(section))
        {
            section = await _uci.AddSectionAsync(ip, session, "wireless", "wifi-iface", null, cancellationToken);
        }

        var values = new Dictionary<string, object>
        {
            { "device", radioName },
            { "mode", "sta" },
            { "wds", 1 },
            { "ssid", remoteSsid },
            { "network", networkName }
        };

        if (!string.IsNullOrWhiteSpace(remotePassword))
        {
            values["encryption"] = "psk2";
            values["key"] = remotePassword;
        }
        else
        {
            values["encryption"] = "none";
            try { await _uci.DeleteAsync(ip, session, "wireless", section, "key", cancellationToken); } catch { }
        }

        try { await _uci.SetOptionAsync(ip, session, "wireless", radioName, "disabled", 0, cancellationToken); } catch { }

        await _uci.SetAsync(ip, session, "wireless", section, values, cancellationToken);
        _logger.LogInformation("تم إعداد الراديو {RadioName} (عميل WDS) بنجاح.", radioName);
    }
}
