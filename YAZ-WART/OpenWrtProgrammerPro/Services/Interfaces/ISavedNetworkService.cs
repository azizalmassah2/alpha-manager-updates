using System.Collections.Generic;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface ISavedNetworkService
    {
        Task<List<SavedNetwork>> GetAllNetworksAsync();
        Task SaveNetworkAsync(SavedNetwork network);
        Task DeleteNetworkAsync(string profileName);
    }
}
