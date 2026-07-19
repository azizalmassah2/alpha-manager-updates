using System.Threading;
using System.Threading.Tasks;

namespace Lux.OpenWrt.Interfaces;

public interface IHostnameConfigurationService
{
    Task ConfigureHostnameAsync(string ip, string session, string targetIp, CancellationToken cancellationToken = default);
}
