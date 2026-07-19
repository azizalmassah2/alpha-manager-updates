using System.Collections.Generic;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface IWirelessService
    {
        Task ConfigureRadioApAsync(string ip, string session, string radioName, string ifaceSection, string ssid, string password, string networkName);
        Task ConfigureRadioStaWdsAsync(string ip, string session, string radioName, string ifaceSection, string remoteSsid, string remotePassword, string networkName);
        Task<List<ScanResult>> ScanNetworksAsync(string ip, string session, string radioName);
    }
}
