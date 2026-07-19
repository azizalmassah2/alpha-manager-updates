using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Services;

public class DiscoveredDevice
{
    public string Identity { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
    public string RouterBoard { get; set; } = string.Empty;
}

public interface IMikroTikDiscoveryService
{
    Task<IEnumerable<DiscoveredDevice>> DiscoverDevicesAsync();
}
