using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lux.OpenWrt.Interfaces;

public interface IUciService
{
    Task<JsonElement> GetAsync(string ip, string session, string config, string? section = null, string? option = null, CancellationToken cancellationToken = default);
    Task SetAsync(string ip, string session, string config, string section, Dictionary<string, object> values, CancellationToken cancellationToken = default);
    Task SetOptionAsync(string ip, string session, string config, string section, string option, object value, CancellationToken cancellationToken = default);
    Task<string> AddSectionAsync(string ip, string session, string config, string type, string? name = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string ip, string session, string config, string? section = null, string? option = null, CancellationToken cancellationToken = default);
    Task CommitAsync(string ip, string session, string config, CancellationToken cancellationToken = default);
    Task ApplyAsync(string ip, string session, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> GetConfigDictAsync(string ip, string session, string config, CancellationToken cancellationToken = default);
    Task RevertAsync(string ip, string session, string config, CancellationToken cancellationToken = default);
}
