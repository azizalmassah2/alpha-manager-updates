using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using OpenWrtProgrammerPro.Helpers;
using OpenWrtProgrammerPro.Models;
using OpenWrtProgrammerPro.Services.Interfaces;

namespace OpenWrtProgrammerPro.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly MainViewModel _mainVm;
        private readonly ISavedNetworkService _savedNetworksService;

        // Base Network Settings
        private string _baseSsid24G;
        private string _baseSsid5G;
        private string _wifiPassword;
        private bool _isWifiEncrypted;
        private string _baseGateway;
        private string _baseSubnet;
        private string _hostnamePrefix;
        private string _networkPrefix;
        private int _startingModemNumber;

        // Final Password Settings fields
        private string _connectUsername = "root";
        private string _connectPassword = "";
        private string _newPassword = "";
        private string _confirmNewPassword = "";
        private bool _changePasswordAfterProgramming;
        private bool _tryNetworkPasswordFirst;

        // Upstream Profile Manager fields
        private ObservableCollection<SavedNetwork> _savedNetworks = new();
        private SavedNetwork? _selectedSavedNetwork;
        private string _profileNameInput = "";
        private string _remoteSsidInput = "";
        private string _remotePasswordInput = "";
        private string _notesInput = "";

        public SettingsViewModel(MainViewModel mainVm)
        {
            _mainVm = mainVm;
            _savedNetworksService = ServiceLocator.Instance.Resolve<ISavedNetworkService>();

            // Copy settings from MainViewModel
            _baseSsid24G = mainVm.BaseSsid24G;
            _baseSsid5G = mainVm.BaseSsid5G;
            _wifiPassword = mainVm.WifiPassword;
            _isWifiEncrypted = mainVm.IsWifiEncrypted;
            _baseGateway = mainVm.BaseGateway;
            _baseSubnet = mainVm.BaseSubnet;
            _hostnamePrefix = mainVm.HostnamePrefix;
            _networkPrefix = mainVm.NetworkPrefix;
            _startingModemNumber = mainVm.StartingModemNumber;

            _connectUsername = mainVm.ConnectUsername;
            _connectPassword = mainVm.ConnectPassword;
            _newPassword = mainVm.NewPassword;
            _confirmNewPassword = mainVm.NewPassword;
            _changePasswordAfterProgramming = mainVm.ChangePasswordAfterProgramming;
            _tryNetworkPasswordFirst = mainVm.TryNetworkPasswordFirst;

            // Load saved networks list
            LoadSavedNetworks();

            // Commands
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            CancelCommand = new RelayCommand(Cancel);
            
            SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync);
            DeleteProfileCommand = new AsyncRelayCommand(DeleteProfileAsync, () => SelectedSavedNetwork != null);
            NewProfileCommand = new RelayCommand(ClearProfileInputs);
        }

        public string BaseSsid24G
        {
            get => _baseSsid24G;
            set => SetProperty(ref _baseSsid24G, value);
        }

        public string BaseSsid5G
        {
            get => _baseSsid5G;
            set => SetProperty(ref _baseSsid5G, value);
        }

        public string WifiPassword
        {
            get => _wifiPassword;
            set => SetProperty(ref _wifiPassword, value);
        }

        public bool IsWifiEncrypted
        {
            get => _isWifiEncrypted;
            set => SetProperty(ref _isWifiEncrypted, value);
        }

        public string BaseGateway
        {
            get => _baseGateway;
            set => SetProperty(ref _baseGateway, value);
        }

        public string BaseSubnet
        {
            get => _baseSubnet;
            set => SetProperty(ref _baseSubnet, value);
        }

        public string HostnamePrefix
        {
            get => _hostnamePrefix;
            set => SetProperty(ref _hostnamePrefix, value);
        }

        public string NetworkPrefix
        {
            get => _networkPrefix;
            set => SetProperty(ref _networkPrefix, value);
        }

        public int StartingModemNumber
        {
            get => _startingModemNumber;
            set => SetProperty(ref _startingModemNumber, value);
        }

        public string ConnectUsername
        {
            get => _connectUsername;
            set => SetProperty(ref _connectUsername, value);
        }

        public string ConnectPassword
        {
            get => _connectPassword;
            set => SetProperty(ref _connectPassword, value);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmNewPassword
        {
            get => _confirmNewPassword;
            set => SetProperty(ref _confirmNewPassword, value);
        }

        public bool ChangePasswordAfterProgramming
        {
            get => _changePasswordAfterProgramming;
            set => SetProperty(ref _changePasswordAfterProgramming, value);
        }

        public bool TryNetworkPasswordFirst
        {
            get => _tryNetworkPasswordFirst;
            set => SetProperty(ref _tryNetworkPasswordFirst, value);
        }

        public ObservableCollection<SavedNetwork> SavedNetworks
        {
            get => _savedNetworks;
            set => SetProperty(ref _savedNetworks, value);
        }

        public SavedNetwork? SelectedSavedNetwork
        {
            get => _selectedSavedNetwork;
            set
            {
                if (SetProperty(ref _selectedSavedNetwork, value) && value != null)
                {
                    ProfileNameInput = value.ProfileName;
                    RemoteSsidInput = value.Ssid;
                    RemotePasswordInput = value.Password;
                    NotesInput = value.Notes;
                }
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ProfileNameInput
        {
            get => _profileNameInput;
            set => SetProperty(ref _profileNameInput, value);
        }

        public string RemoteSsidInput
        {
            get => _remoteSsidInput;
            set => SetProperty(ref _remoteSsidInput, value);
        }

        public string RemotePasswordInput
        {
            get => _remotePasswordInput;
            set => SetProperty(ref _remotePasswordInput, value);
        }

        public string NotesInput
        {
            get => _notesInput;
            set => SetProperty(ref _notesInput, value);
        }

        public RelayCommand SaveSettingsCommand { get; }
        public RelayCommand CancelCommand { get; }
        
        public AsyncRelayCommand SaveProfileCommand { get; }
        public AsyncRelayCommand DeleteProfileCommand { get; }
        public RelayCommand NewProfileCommand { get; }

        public event EventHandler<bool>? RequestClose;

        private void LoadSavedNetworks()
        {
            // Copy profiles from MainViewModel to settings window list
            SavedNetworks.Clear();
            foreach (var profile in _mainVm.SavedNetworks)
            {
                SavedNetworks.Add(new SavedNetwork
                {
                    ProfileName = profile.ProfileName,
                    Ssid = profile.Ssid,
                    Password = profile.Password,
                    Notes = profile.Notes
                });
            }
        }

        private void SaveSettings()
        {
            // Validate starting modem number
            if (StartingModemNumber < 0 || StartingModemNumber > 254 || StartingModemNumber == 255)
            {
                MessageBox.Show("Please enter a valid starting modem number (0-254, except 255).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NetworkPrefix))
            {
                MessageBox.Show("Please enter a network prefix.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate new passwords match if ChangePasswordAfterProgramming is enabled
            if (ChangePasswordAfterProgramming)
            {
                if (string.IsNullOrEmpty(NewPassword))
                {
                    MessageBox.Show("يرجى إدخال كلمة المرور الموحدة للشبكة.", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (NewPassword != ConfirmNewPassword)
                {
                    MessageBox.Show("كلمة المرور الموحدة للشبكة غير مطابقة لتأكيد كلمة المرور.", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Apply changes back to MainViewModel
            _mainVm.BaseSsid24G = BaseSsid24G;
            _mainVm.BaseSsid5G = BaseSsid5G;
            _mainVm.WifiPassword = WifiPassword;
            _mainVm.IsWifiEncrypted = IsWifiEncrypted;
            _mainVm.BaseGateway = BaseGateway;
            _mainVm.BaseSubnet = BaseSubnet;
            _mainVm.HostnamePrefix = HostnamePrefix;
            _mainVm.NetworkPrefix = NetworkPrefix;
            _mainVm.StartingModemNumber = StartingModemNumber;

            _mainVm.ConnectUsername = ConnectUsername;
            _mainVm.ConnectPassword = ConnectPassword;
            _mainVm.NewPassword = NewPassword;
            _mainVm.ChangePasswordAfterProgramming = ChangePasswordAfterProgramming;
            _mainVm.TryNetworkPasswordFirst = TryNetworkPasswordFirst;

            // Trigger immediate settings save on disk
            Task.Run(async () =>
            {
                await _mainVm.SaveSettingsAsync();
            });

            RequestClose?.Invoke(this, true);
        }

        private void Cancel()
        {
            RequestClose?.Invoke(this, false);
        }

        private void ClearProfileInputs()
        {
            SelectedSavedNetwork = null;
            ProfileNameInput = "";
            RemoteSsidInput = "";
            RemotePasswordInput = "";
            NotesInput = "";
        }

        private async Task SaveProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(ProfileNameInput) || string.IsNullOrWhiteSpace(RemoteSsidInput))
            {
                MessageBox.Show("Please enter a profile name and SSID.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var net = new SavedNetwork
            {
                ProfileName = ProfileNameInput,
                Ssid = RemoteSsidInput,
                Password = RemotePasswordInput,
                Notes = NotesInput
            };

            await _savedNetworksService.SaveNetworkAsync(net);
            
            // Reload Main VM profiles and local list
            await _mainVm.LoadSavedNetworksListAsync();
            LoadSavedNetworks();
            ClearProfileInputs();
        }

        private async Task DeleteProfileAsync()
        {
            if (SelectedSavedNetwork == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete profile '{SelectedSavedNetwork.ProfileName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await _savedNetworksService.DeleteNetworkAsync(SelectedSavedNetwork.ProfileName);
                
                // Reload Main VM profiles and local list
                await _mainVm.LoadSavedNetworksListAsync();
                LoadSavedNetworks();
                ClearProfileInputs();
            }
        }
    }
}
