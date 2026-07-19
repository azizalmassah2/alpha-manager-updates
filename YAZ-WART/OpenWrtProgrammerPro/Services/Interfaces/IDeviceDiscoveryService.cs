using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface IDeviceDiscoveryService
    {
        Task<DeviceDiscoveryResult> DiscoverDeviceAsync(string ip, string session);
    }
}
