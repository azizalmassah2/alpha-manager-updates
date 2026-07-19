using System.Threading;
using System.Threading.Tasks;

namespace Lux.OpenWrt.Interfaces;

public interface INetworkConfigurationService
{
    Task SetLanIpAsync(string ip, string session, string lanSection, string ipaddr, string gateway, string netmask, CancellationToken cancellationToken = default);
    Task DisableDhcpAsync(string ip, string session, string lanSection, CancellationToken cancellationToken = default);
}
