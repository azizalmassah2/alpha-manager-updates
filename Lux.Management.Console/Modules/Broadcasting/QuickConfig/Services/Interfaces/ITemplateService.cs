using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces
{
    public interface ITemplateService
    {
        Task SaveTemplateAsync(DeviceTemplate template);
        Task<DeviceTemplate> LoadTemplateAsync(string name);
        Task DeleteTemplateAsync(string name);
        Task<List<DeviceTemplate>> GetAllTemplatesAsync();
    }
}
