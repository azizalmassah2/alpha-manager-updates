using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models
{
    public enum WirelessMode
    {
        AccessPoint,
        ClientWds
    }

    public class WirelessConfig : ObservableObject
    {
        private string _ssid24Ghz = string.Empty;
        private string _ssid5Ghz = string.Empty;
        private string _wifiPassword = string.Empty;
        private bool _isEncrypted = false;
        private WirelessMode _mode = WirelessMode.AccessPoint;
        private string _remoteSsid = string.Empty;
        private string _remotePassword = string.Empty;

        public string Ssid24Ghz
        {
            get => _ssid24Ghz;
            set => SetProperty(ref _ssid24Ghz, value);
        }

        public string Ssid5Ghz
        {
            get => _ssid5Ghz;
            set => SetProperty(ref _ssid5Ghz, value);
        }

        public string WifiPassword
        {
            get => _wifiPassword;
            set => SetProperty(ref _wifiPassword, value);
        }

        public bool IsEncrypted
        {
            get => _isEncrypted;
            set => SetProperty(ref _isEncrypted, value);
        }

        public WirelessMode Mode
        {
            get => _mode;
            set
            {
                if (SetProperty(ref _mode, value))
                {
                    OnPropertyChanged(nameof(IsClientWds));
                    OnPropertyChanged(nameof(IsAccessPoint));
                }
            }
        }

        public string RemoteSsid
        {
            get => _remoteSsid;
            set => SetProperty(ref _remoteSsid, value);
        }

        public string RemotePassword
        {
            get => _remotePassword;
            set => SetProperty(ref _remotePassword, value);
        }

        public bool IsClientWds => Mode == WirelessMode.ClientWds;
        public bool IsAccessPoint => Mode == WirelessMode.AccessPoint;
    }
}
