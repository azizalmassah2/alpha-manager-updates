using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Models;

namespace Lux.OpenWrt.Interfaces;

public interface IUbusClient
{
    Task<string> LoginAsync(string ip, string username, string password, CancellationToken cancellationToken = default);
    Task<(string Session, DeviceAcls Acls)> LoginWithAclsAsync(string ip, string username, string password, CancellationToken cancellationToken = default);
    Task<JsonElement> CallAsync(string ip, string session, string ubusObject, string method, object? args, CancellationToken cancellationToken = default);
    Task<Dictionary<string, JsonElement>> ListAsync(string ip, string session, string? pattern = null, CancellationToken cancellationToken = default);
}
