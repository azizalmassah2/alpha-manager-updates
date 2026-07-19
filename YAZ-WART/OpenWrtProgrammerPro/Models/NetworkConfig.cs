using OpenWrtProgrammerPro.Helpers;

namespace OpenWrtProgrammerPro.Models
{
    public class NetworkConfig : ObservableObject
    {
        private string _newIpAddress = string.Empty;
        private string _gateway = string.Empty;
        private string _subnetMask = "255.255.255.0";
        private int _vlanId = 1;

        public string NewIpAddress
        {
            get => _newIpAddress;
            set
            {
                if (SetProperty(ref _newIpAddress, value))
                {
                    OnPropertyChanged(nameof(GeneratedHostname));
                }
            }
        }

        public string Gateway
        {
            get => _gateway;
            set => SetProperty(ref _gateway, value);
        }

        public string SubnetMask
        {
            get => _subnetMask;
            set => SetProperty(ref _subnetMask, value);
        }

        public int VlanId
        {
            get => _vlanId;
            set => SetProperty(ref _vlanId, value);
        }

        public string GeneratedHostname => HostnameGenerator.Generate(NewIpAddress);
    }
}
