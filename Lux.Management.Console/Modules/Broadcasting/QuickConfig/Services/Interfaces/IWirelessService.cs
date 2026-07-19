using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces
{
    public interface IWirelessService
    {
        Task ConfigureRadioApAsync(string ip, string session, string radioName, string ifaceSection, string ssid, string password, string networkName);
        Task ConfigureRadioStaWdsAsync(string ip, string session, string radioName, string ifaceSection, string remoteSsid, string remotePassword, string networkName);
        Task<List<ScanResult>> ScanNetworksAsync(string ip, string session, string radioName);
    }
}
