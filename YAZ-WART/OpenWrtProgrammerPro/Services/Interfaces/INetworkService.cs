using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface INetworkService
    {
        Task SetLanIpAsync(string ip, string session, string lanSection, string ipaddr, string gateway, string netmask);
        Task CreateVlanAsync(string ip, string session, string lanDevice, VlanArchitecture vlanType, int vlanId, string switchName, string switchCpuPort, string switchLanPorts);
        Task DisableDhcpAsync(string ip, string session, string lanSection);
    }
}
