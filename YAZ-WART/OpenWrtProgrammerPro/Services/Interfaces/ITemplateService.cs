using System.Collections.Generic;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface ITemplateService
    {
        Task SaveTemplateAsync(DeviceTemplate template);
        Task<DeviceTemplate> LoadTemplateAsync(string name);
        Task DeleteTemplateAsync(string name);
        Task<List<DeviceTemplate>> GetAllTemplatesAsync();
    }
}
