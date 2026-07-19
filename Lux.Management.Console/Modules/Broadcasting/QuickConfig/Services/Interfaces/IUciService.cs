using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces
{
    public interface IUciService
    {
        Task<JsonElement> GetAsync(string ip, string session, string config, string? section = null, string? option = null);
        Task SetAsync(string ip, string session, string config, string section, Dictionary<string, object> values);
        Task SetOptionAsync(string ip, string session, string config, string section, string option, object value);
        Task<string> AddSectionAsync(string ip, string session, string config, string type, string? name = null);
        Task DeleteAsync(string ip, string session, string config, string? section = null, string? option = null);
        Task CommitAsync(string ip, string session, string config);
        Task ApplyAsync(string ip, string session);
        
        // Helper to get an entire config file as a dictionary of section names to section objects
        Task<Dictionary<string, object>> GetConfigDictAsync(string ip, string session, string config);
    }
}
