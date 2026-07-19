using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces
{
    public interface IDeviceDiscoveryService
    {
        Task<DeviceDiscoveryResult> DiscoverDeviceAsync(string ip, string session);
    }
}
