using System.Threading;
using System.Threading.Tasks;

namespace Lux.OpenWrt.Interfaces;

public interface IVlanConfigurationService
{
    Task CreateVlanAsync(string ip, string session, string lanDevice, string vlanTypeStr, int vlanId, string switchName, string switchCpuPort, string switchLanPorts, CancellationToken cancellationToken = default);
}
