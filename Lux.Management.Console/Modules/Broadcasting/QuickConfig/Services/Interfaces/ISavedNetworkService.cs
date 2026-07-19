using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces
{
    public interface ISavedNetworkService
    {
        Task<List<SavedNetwork>> GetAllNetworksAsync();
        Task SaveNetworkAsync(SavedNetwork network);
        Task DeleteNetworkAsync(string profileName);
    }
}
