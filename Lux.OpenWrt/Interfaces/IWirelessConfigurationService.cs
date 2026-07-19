using System.Threading;
using System.Threading.Tasks;

namespace Lux.OpenWrt.Interfaces;

public interface IWirelessConfigurationService
{
    Task ConfigureRadioApAsync(string ip, string session, string radioName, string ifaceSection, string ssid, string password, string networkName, CancellationToken cancellationToken = default);
    Task ConfigureRadioStaWdsAsync(string ip, string session, string radioName, string ifaceSection, string remoteSsid, string remotePassword, string networkName, CancellationToken cancellationToken = default);
}
