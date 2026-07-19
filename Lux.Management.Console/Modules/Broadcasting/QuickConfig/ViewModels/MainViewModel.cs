using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Views;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        // Services
        private readonly IUbusClient _ubusClient;
        private readonly ILoggerService _logger;
        private readonly ISavedNetworkService _savedNetworksService;
        private readonly ITemplateService _templateService;
        private readonly IProgrammingService _programmingService;
        private readonly IDeviceDiscoveryService _discoveryService;
        private readonly AppSettingsService _appSettingsService;
        
        // Connection properties
        private string _connectIp = "192.168.1.1";
        private string _connectUsername = "root";
        private string _connectPassword = "";
        private DeviceInfo _connectedDevice = new();
        private bool _isConnected;

        // Base network configurations (configured once)
        private string _baseSsid24G = "LUX-4G";
        private string _baseSsid5G = "LUX-5G";
        private string _baseGateway = "10.0.0.1";
        private string _baseSubnet = "255.255.0.0";
        private string _hostnamePrefix = "YAZ";
        private string _networkPrefix = "10.0.0";
        private int _startingModemNumber = 2;
        private int _lastProgrammedModemNumber = 0;

        // Wireless properties
        private string _wifiPassword = "";
        private WirelessMode _selectedMode = WirelessMode.AccessPoint;
        private string _remoteSsid = "";
        private string _remotePassword = "";

        // Daily operation inputs
        private int _modemNumber = 2;
        private string _modemValidationError = "";

        // Saved networks manager
        private ObservableCollection<SavedNetwork> _savedNetworks = new();
        private SavedNetwork? _selectedSavedNetwork;
        private string _profileNameInput = "";
        private string _notesInput = "";

        // Obsolete templates manager (kept for code compatibility, hidden in UI)
        private ObservableCollection<DeviceTemplate> _templates = new();
        private DeviceTemplate? _selectedTemplate;
        private string _templateNameInput = "";

        // Device target properties (for single programming view)
        private bool _isProgramming;
        private int _progressPercent;
        private string _progressMessage = "";
        private int _completedCount;
        private int _failedCount;
        private int _totalCount;

        // Cancellations
        private CancellationTokenSource? _programmingCts;

        public MainViewModel()
        {
            // Resolve services
            _ubusClient = ServiceLocator.Instance.Resolve<IUbusClient>();
            _logger = ServiceLocator.Instance.Resolve<ILoggerService>();
            _savedNetworksService = ServiceLocator.Instance.Resolve<ISavedNetworkService>();
            _templateService = ServiceLocator.Instance.Resolve<ITemplateService>();
            _programmingService = ServiceLocator.Instance.Resolve<IProgrammingService>();
            _discoveryService = ServiceLocator.Instance.Resolve<IDeviceDiscoveryService>();
            _appSettingsService = new AppSettingsService();

            // Initialize collections
            LogEntries = _logger.Entries;

            // Load Saved Data & Settings
            Task.Run(async () =>
            {
                await LoadSavedNetworksListAsync();
                await LoadTemplatesListAsync();
                await LoadSettingsAsync();
            });

            // Bind Commands
            ConnectCommand = new AsyncRelayCommand(ConnectDeviceAsync, () => !IsConnected && !IsProgramming);
            DisconnectCommand = new RelayCommand(DisconnectDevice, () => IsConnected && !IsProgramming);
            ProgramDeviceCommand = new AsyncRelayCommand(StartProgrammingSingleAsync, () => !IsProgramming && IsModemNumberValid);
            CancelProgrammingCommand = new RelayCommand(CancelProgramming, () => IsProgramming);
            
            ClearLogsCommand = new RelayCommand(() => _logger.Clear());
            ExportLogsTxtCommand = new AsyncRelayCommand(ExportLogsTxtAsync);
            ExportLogsJsonCommand = new AsyncRelayCommand(ExportLogsJsonAsync);
            
            ScanNetworksCommand = new AsyncRelayCommand(ScanWirelessNetworksAsync, () => IsConnected && !IsProgramming);
            
            SaveSavedNetworkCommand = new AsyncRelayCommand(SaveNetworkProfileAsync);
            DeleteSavedNetworkCommand = new AsyncRelayCommand(DeleteNetworkProfileAsync, () => SelectedSavedNetwork != null);
            LoadSavedNetworkCommand = new RelayCommand(LoadSelectedNetworkProfile, () => SelectedSavedNetwork != null);
            
            SaveTemplateCommand = new AsyncRelayCommand(SaveDeviceTemplateAsync);
            DeleteTemplateCommand = new AsyncRelayCommand(DeleteDeviceTemplateAsync, () => SelectedTemplate != null);
            LoadTemplateCommand = new RelayCommand(LoadSelectedDeviceTemplate, () => SelectedTemplate != null);

            OpenSettingsCommand = new RelayCommand(OpenSettings);
            NextNumberCommand = new RelayCommand(SuggestNextNumber);
            OpenLicenseInfoCommand = new RelayCommand(OpenLicenseInfo);
        }

        #region Properties

        public string ConnectIp
        {
            get => _connectIp;
            set => SetProperty(ref _connectIp, value);
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

        public DeviceInfo ConnectedDevice
        {
            get => _connectedDevice;
            set => SetProperty(ref _connectedDevice, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
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

        public int LastProgrammedModemNumber
        {
            get => _lastProgrammedModemNumber;
            set
            {
                if (SetProperty(ref _lastProgrammedModemNumber, value))
                {
                    OnPropertyChanged(nameof(LastProgrammedText));
                }
            }
        }

        public string LastProgrammedText => LastProgrammedModemNumber > 0 ? LastProgrammedModemNumber.ToString() : "None";

        public int ModemNumber
        {
            get => _modemNumber;
            set
            {
                if (SetProperty(ref _modemNumber, value))
                {
                    ValidateModemNumber();
                    OnPropertyChanged(nameof(GeneratedIp));
                    OnPropertyChanged(nameof(GeneratedVlan));
                    OnPropertyChanged(nameof(GeneratedHostname));
                    OnPropertyChanged(nameof(GeneratedSsid24));
                    OnPropertyChanged(nameof(GeneratedSsid5));
                }
            }
        }

        public string ModemValidationError
        {
            get => _modemValidationError;
            set
            {
                if (SetProperty(ref _modemValidationError, value))
                {
                    OnPropertyChanged(nameof(IsModemNumberValid));
                }
            }
        }

        public bool IsModemNumberValid => string.IsNullOrEmpty(ModemValidationError);

        private void ValidateModemNumber()
        {
            if (ModemNumber == 255)
            {
                ModemValidationError = "This modem number is reserved.";
            }
            else if (ModemNumber < StartingModemNumber)
            {
                ModemValidationError = "This modem number is reserved.";
            }
            else if (ModemNumber < 2 || ModemNumber > 254)
            {
                ModemValidationError = "Modem number must be between 2 and 254.";
            }
            else
            {
                ModemValidationError = "";
            }
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        // Live preview computed properties
        public string GeneratedIp
        {
            get
            {
                var prefix = string.IsNullOrWhiteSpace(NetworkPrefix) ? "10.0.0" : NetworkPrefix.Trim();
                if (prefix.EndsWith('.'))
                {
                    return $"{prefix}{ModemNumber}";
                }
                return $"{prefix}.{ModemNumber}";
            }
        }

        public int GeneratedVlan => ModemNumber;

        public string GeneratedHostname => $"{HostnamePrefix}-{ModemNumber}";

        public string GeneratedSsid24 => $"{BaseSsid24G} {ModemNumber}";

        public string GeneratedSsid5 => $"{BaseSsid5G} {ModemNumber}";

        // Backwards compatibility properties (routed to new fields)
        public string TargetIpsInput
        {
            get => GeneratedIp;
            set { }
        }
        public string Gateway
        {
            get => BaseGateway;
            set => BaseGateway = value;
        }
        public string SubnetMask
        {
            get => BaseSubnet;
            set => BaseSubnet = value;
        }
        public int VlanId
        {
            get => GeneratedVlan;
            set => ModemNumber = value;
        }
        public string Ssid24Ghz
        {
            get => GeneratedSsid24;
            set => BaseSsid24G = value;
        }
        public string Ssid5Ghz
        {
            get => GeneratedSsid5;
            set => BaseSsid5G = value;
        }

        public string WifiPassword
        {
            get => _wifiPassword;
            set => SetProperty(ref _wifiPassword, value);
        }

        private bool _isWifiEncrypted = false;
        public bool IsWifiEncrypted
        {
            get => _isWifiEncrypted;
            set => SetProperty(ref _isWifiEncrypted, value);
        }

        private string _newPassword = "MySecurePassword123";
        private bool _changePasswordAfterProgramming = false;
        private bool _tryNetworkPasswordFirst = false;

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
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

        public WirelessMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (SetProperty(ref _selectedMode, value))
                {
                    OnPropertyChanged(nameof(IsClientWdsMode));
                    OnPropertyChanged(nameof(IsApModeSelected));
                    OnPropertyChanged(nameof(IsClientWdsModeSelected));
                }
            }
        }

        public bool IsClientWdsMode => SelectedMode == WirelessMode.ClientWds;

        public bool IsApModeSelected
        {
            get => SelectedMode == WirelessMode.AccessPoint;
            set
            {
                if (value) SelectedMode = WirelessMode.AccessPoint;
            }
        }

        public bool IsClientWdsModeSelected
        {
            get => SelectedMode == WirelessMode.ClientWds;
            set
            {
                if (value) SelectedMode = WirelessMode.ClientWds;
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
                    NotesInput = value.Notes;
                }
            }
        }

        public string ProfileNameInput
        {
            get => _profileNameInput;
            set => SetProperty(ref _profileNameInput, value);
        }

        public string NotesInput
        {
            get => _notesInput;
            set => SetProperty(ref _notesInput, value);
        }

        public ObservableCollection<DeviceTemplate> Templates
        {
            get => _templates;
            set => SetProperty(ref _templates, value);
        }

        public DeviceTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value) && value != null)
                {
                    TemplateNameInput = value.TemplateName;
                }
            }
        }

        public string TemplateNameInput
        {
            get => _templateNameInput;
            set => SetProperty(ref _templateNameInput, value);
        }

        public ObservableCollection<DeviceTarget> TargetDevices { get; } = new();

        public bool IsProgramming
        {
            get => _isProgramming;
            set => SetProperty(ref _isProgramming, value);
        }

        public int ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set => SetProperty(ref _progressMessage, value);
        }

        public int CompletedCount
        {
            get => _completedCount;
            set => SetProperty(ref _completedCount, value);
        }

        public int FailedCount
        {
            get => _failedCount;
            set => SetProperty(ref _failedCount, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public ObservableCollection<LogEntry> LogEntries { get; }

        #endregion

        #region Commands
 
        public AsyncRelayCommand ConnectCommand { get; }
        public RelayCommand DisconnectCommand { get; }
        public AsyncRelayCommand ProgramDeviceCommand { get; }
        public RelayCommand CancelProgrammingCommand { get; }
        public RelayCommand ClearLogsCommand { get; }
        public AsyncRelayCommand ExportLogsTxtCommand { get; }
        public AsyncRelayCommand ExportLogsJsonCommand { get; }
        public AsyncRelayCommand ScanNetworksCommand { get; }
        public AsyncRelayCommand SaveSavedNetworkCommand { get; }
        public AsyncRelayCommand DeleteSavedNetworkCommand { get; }
        public RelayCommand LoadSavedNetworkCommand { get; }
        public AsyncRelayCommand SaveTemplateCommand { get; }
        public AsyncRelayCommand DeleteTemplateCommand { get; }
        public RelayCommand LoadTemplateCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand NextNumberCommand { get; }
        public RelayCommand OpenLicenseInfoCommand { get; }

        public string LicenseBadgeText => "🛡️ ترخيص نشط";
 
        #endregion

        #region Methods

        private async Task ConnectDeviceAsync()
        {
            _logger.Log($"جاري محاولة الاتصال بـ {ConnectIp}...");
            try
            {
                string session = "";
                DeviceAcls acls = DeviceAcls.FullPermissions();
                bool loginSuccess = false;

                if (TryNetworkPasswordFirst && !string.IsNullOrEmpty(NewPassword))
                {
                    try
                    {
                        (session, acls) = await _ubusClient.LoginWithAclsAsync(ConnectIp, ConnectUsername, NewPassword);
                        loginSuccess = true;
                    }
                    catch
                    {
                        // Fallback to default
                    }
                }

                if (!loginSuccess)
                {
                    (session, acls) = await _ubusClient.LoginWithAclsAsync(ConnectIp, ConnectUsername, ConnectPassword);
                }

                // ── التحقق من الحد الأدنى المطلوب للعمل ──────────────────────────────
                if (!acls.CanGet || !acls.CanSet)
                {
                    throw new Exception(
                        $"الجهاز لا يمنح الحد الأدنى من الصلاحيات المطلوبة للبرمجة.\n" +
                        $"المطلوب: uci.get + uci.set\n" +
                        $"الممنوح: get={acls.CanGet}, set={acls.CanSet}");
                }

                _logger.LogSuccess($"[ACL] التحقق من الصلاحيات اكتمل. الوضع: {acls.ProgrammingMode}");

                if (!acls.CanCommit)
                    _logger.LogWarning("[ACL] uci.commit غير مصرح به — التغييرات ستُكتب في الذاكرة فقط (runtime).");
                if (!acls.CanApply)
                    _logger.LogWarning("[ACL] uci.apply غير مصرح به — الخدمات لن تُعاد تشغيلها تلقائياً.");

                // ── جلب اسم المضيف والإصدار ───────────────────────────────────────────
                string hostname = "OpenWrt";
                string version  = "23.x";

                try
                {
                    await _ubusClient.CallAsync(ConnectIp, session, "system", "info", null);
                    var systemUci = await ServiceLocator.Instance.Resolve<IUciService>().GetConfigDictAsync(ConnectIp, session, "system");
                    foreach (var sVal in systemUci.Values)
                    {
                        if (sVal is Dictionary<string, object> sDict &&
                            sDict.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "system" &&
                            sDict.TryGetValue("hostname", out var hostVal))
                        {
                            hostname = hostVal.ToString() ?? hostname;
                        }
                    }
                }
                catch { }

                try
                {
                    var releaseInfo = await _ubusClient.CallAsync(ConnectIp, session, "system", "board", null);
                    if (releaseInfo.TryGetProperty("release", out var relProp) &&
                        relProp.TryGetProperty("description", out var descProp))
                    {
                        version = descProp.GetString() ?? version;
                    }
                }
                catch { }

                ConnectedDevice = new DeviceInfo
                {
                    Hostname = hostname,
                    IpAddress = ConnectIp,
                    OpenWrtVersion = version,
                    SessionId = session,
                    SessionStatus = "متصل بنجاح",
                    IsConnected = true,
                    Acls = acls
                };

                IsConnected = true;
                _logger.LogSuccess($"[OK] تم الاتصال بنجاح بجهاز {hostname} ({version}) — {ConnectedDevice.ProgrammingMode}");

            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل الاتصال: {ex.Message}");
                MessageBox.Show($"حدث خطأ أثناء الاتصال بالجهاز:\n{ex.Message}", "خطأ في الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                DisconnectDevice();
            }
        }

        private void DisconnectDevice()
        {
            ConnectedDevice = new DeviceInfo
            {
                SessionStatus = "غير متصل"
            };
            IsConnected = false;
            _logger.Log("تم قطع الاتصال بالجهاز.");
        }

        private async Task ScanWirelessNetworksAsync()
        {
            if (!IsConnected || string.IsNullOrEmpty(ConnectedDevice.SessionId))
            {
                MessageBox.Show("يرجى الاتصال بالجهاز أولاً قبل محاولة فحص الشبكات.", "غير متصل", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _logger.Log("جاري فحص الشبكات اللاسلكية، الرجاء الانتظار...");
                
                // Discover radios first to see which radio is 5GHz (since scanning is for client WDS which connects via 5GHz)
                var result = await _discoveryService.DiscoverDeviceAsync(ConnectIp, ConnectedDevice.SessionId);
                var targetRadio = result.Radio5GhzName; // Standard 5GHz radio is radio1 or discovered name

                var scanList = await ServiceLocator.Instance.Resolve<IWirelessService>().ScanNetworksAsync(ConnectIp, ConnectedDevice.SessionId, targetRadio);
                
                if (scanList == null || scanList.Count == 0)
                {
                    MessageBox.Show("لم يتم العثور على أي شبكات لاسلكية محيطة، أو أن جهاز الراديو مشغول.", "لا توجد نتائج", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Show scan window
                var scanVm = new ScanNetworksViewModel(scanList);
                var scanWindow = new ScanNetworksWindow
                {
                    DataContext = scanVm,
                    Owner = Application.Current.MainWindow
                };

                if (scanWindow.ShowDialog() == true && scanVm.SelectedNetwork != null)
                {
                    RemoteSsid = scanVm.SelectedNetwork.Ssid;
                    _logger.Log($"تم اختيار الشبكة {RemoteSsid} من فحص الشبكات.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل فحص الشبكات: {ex.Message}");
                MessageBox.Show($"فشل فحص الشبكات اللاسلكية:\n{ex.Message}", "خطأ في الفحص", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartProgrammingSingleAsync()
        {
            // Validate Modem Number
            if (ModemNumber == 255)
            {
                MessageBox.Show("رقم المودم هذا محجوز ولا يمكن استخدامه (عنوان البث).", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ModemNumber < StartingModemNumber)
            {
                MessageBox.Show($"رقم المودم هذا محجوز (يجب أن يكون {StartingModemNumber} أو أكثر).", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ModemNumber < 2 || ModemNumber > 254)
            {
                MessageBox.Show("يرجى إدخال رقم مودم صالح بين 2 و 254.", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsProgramming = true;
            _programmingCts = new CancellationTokenSource();
            var token = _programmingCts.Token;

            // Generate values
            string targetIp = GeneratedIp;
            string gateway = BaseGateway;
            string subnetMask = BaseSubnet;
            int vlanId = ModemNumber;

            _logger.Log($"[البدء] بدء برمجة الجهاز ({ConnectIp}) بالقيم المولدة تلقائياً...");
            _logger.Log($"العنوان المستهدف: {targetIp} | معرّف VLAN: {vlanId} | المضيف: {GeneratedHostname}");

            try
            {
                var wirelessConfig = new WirelessConfig
                {
                    Ssid24Ghz = GeneratedSsid24,
                    Ssid5Ghz = GeneratedSsid5,
                    IsEncrypted = IsWifiEncrypted,
                    WifiPassword = WifiPassword,
                    Mode = SelectedMode,
                    RemoteSsid = RemoteSsid,
                    RemotePassword = RemotePassword
                };

                var progress = new Progress<(int percent, string message)>(p =>
                {
                    ProgressPercent = p.percent;
                    ProgressMessage = p.message;
                });

                // Run programming
                await _programmingService.ProgramDeviceSingleAsync(
                    ConnectIp,
                    ConnectUsername,
                    ConnectPassword,
                    targetIp,
                    gateway,
                    subnetMask,
                    vlanId,
                    wirelessConfig,
                    progress,
                    token,
                    IsConnected ? ConnectedDevice.CanCommit : false,
                    IsConnected ? ConnectedDevice.CanApply : false,
                    ChangePasswordAfterProgramming,
                    NewPassword,
                    TryNetworkPasswordFirst
                );

                CompletedCount = 1;
                FailedCount = 0;
                _logger.LogSuccess($"[مكتمل] تمت برمجة الجهاز بنجاح!");

                // Store last programmed number and save settings
                LastProgrammedModemNumber = ModemNumber;
                await SaveSettingsAsync();

                // Suggest next number
                SuggestNextNumber();
            }
            catch (Exception ex)
            {
                CompletedCount = 0;
                FailedCount = 1;
                _logger.LogError($"[فشل] حدث خطأ أثناء برمجة الجهاز: {ex.Message}");
                MessageBox.Show($"فشلت برمجة الجهاز:\n{ex.Message}", "خطأ في البرمجة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProgramming = false;
                ProgressPercent = 0;
                ProgressMessage = "";
                _programmingCts = null;
            }
        }

        private void OpenSettings()
        {
            var settingsVm = new SettingsViewModel(this);
            var settingsWindow = new SettingsWindow
            {
                DataContext = settingsVm,
                Owner = Application.Current.MainWindow
            };
            if (settingsWindow.ShowDialog() == true)
            {
                // Refresh previews
                OnPropertyChanged(nameof(GeneratedIp));
                OnPropertyChanged(nameof(GeneratedVlan));
                OnPropertyChanged(nameof(GeneratedHostname));
                OnPropertyChanged(nameof(GeneratedSsid24));
                OnPropertyChanged(nameof(GeneratedSsid5));
                ValidateModemNumber();
            }
        }

        private void OpenLicenseInfo()
        {
            System.Windows.MessageBox.Show(
                "البرنامج مرخص ومفعل بالكامل.",
                "معلومات الترخيص",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void SuggestNextNumber()
        {
            int next = LastProgrammedModemNumber + 1;
            if (next < StartingModemNumber)
            {
                next = StartingModemNumber;
            }
            while (next == 255)
            {
                next++;
            }
            if (next > 254)
            {
                next = StartingModemNumber;
            }
            ModemNumber = next;
        }

        private async Task StartProgrammingBatchAsync()
        {
            var parsedIps = IpRangeParser.Parse(TargetIpsInput);
            if (parsedIps.Count == 0)
            {
                MessageBox.Show("يرجى إدخال عناوين IP مستهدفة صالحة.", "مدخلات غير صالحة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Populate TargetDevices list for UI
            TargetDevices.Clear();
            foreach (var ip in parsedIps)
            {
                TargetDevices.Add(new DeviceTarget { IpAddress = ip, Status = TargetStatus.Pending });
            }

            TotalCount = TargetDevices.Count;
            CompletedCount = 0;
            FailedCount = 0;

            // Generate previews string
            var hostnamesList = parsedIps.Select(ip => HostnameGenerator.Generate(ip)).ToList();

            // Prepare Preview Window
            var preview = new ProgrammingPreview
            {
                TargetIps = string.Join(", ", parsedIps),
                Hostnames = string.Join(", ", hostnamesList),
                Gateway = Gateway,
                SubnetMask = SubnetMask,
                VlanId = VlanId.ToString(),
                Ssid24Ghz = Ssid24Ghz,
                Ssid5Ghz = Ssid5Ghz,
                WifiPassword = WifiPassword,
                ModeText = SelectedMode == WirelessMode.AccessPoint ? "Access Point (نقطة وصول)" : "Client WDS (عميل WDS)",
                IsClientWds = SelectedMode == WirelessMode.ClientWds,
                RemoteSsid = RemoteSsid,
                RemotePassword = RemotePassword
            };

            var previewWindow = new PreviewWindow(preview)
            {
                Owner = Application.Current.MainWindow
            };

            if (previewWindow.ShowDialog() != true)
            {
                _logger.Log("تم إلغاء عملية البرمجة من قبل المستخدم قبل البدء.");
                return;
            }

            IsProgramming = true;
            _programmingCts = new CancellationTokenSource();
            var token = _programmingCts.Token;

            _logger.Log($"[البدء] بدء برمجة {TotalCount} جهاز(أجهزة) بالتسلسل...");

            try
            {
                var wirelessConfig = new WirelessConfig
                {
                    Ssid24Ghz = Ssid24Ghz,
                    Ssid5Ghz = Ssid5Ghz,
                    IsEncrypted = IsWifiEncrypted,
                    WifiPassword = WifiPassword,
                    Mode = SelectedMode,
                    RemoteSsid = RemoteSsid,
                    RemotePassword = RemotePassword
                };

                for (int i = 0; i < TargetDevices.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var target = TargetDevices[i];
                    target.Status = TargetStatus.InProgress;
                    
                    var progress = new Progress<(int percent, string message)>(p =>
                    {
                        ProgressPercent = p.percent;
                        ProgressMessage = $"الجهاز {i + 1}/{TotalCount}: {p.message}";
                    });

                    _logger.Log($"[جهاز {i + 1}/{TotalCount}] جاري الاتصال وبرمجة الجهاز على عنوان IP المستهدف: {target.IpAddress}...");

                    try
                    {
                        // Connect to the device via ConnectIp, but program it to target.IpAddress
                        await _programmingService.ProgramDeviceSingleAsync(
                            ConnectIp,
                            ConnectUsername,
                            ConnectPassword,
                            target.IpAddress,
                            Gateway,
                            SubnetMask,
                            VlanId,
                            wirelessConfig,
                            progress,
                            token,
                            ConnectedDevice.CanCommit,
                            ConnectedDevice.CanApply,
                            ChangePasswordAfterProgramming,
                            NewPassword,
                            TryNetworkPasswordFirst);

                        target.Status = TargetStatus.Success;
                        CompletedCount++;
                        _logger.LogSuccess($"[جهاز {i + 1}/{TotalCount}] اكتملت البرمجة للجهاز بنجاح.");

                        // If there are more devices, ask user to switch device
                        if (i < TargetDevices.Count - 1 && !token.IsCancellationRequested)
                        {
                            _logger.LogWarning("يرجى فصل الجهاز الحالي المبرمج، وتوصيل الجهاز الجديد غير المبرمج بالشبكة.");
                            
                            ProgressMessage = "في انتظار توصيل الجهاز التالي...";
                            ProgressPercent = 0;

                            // We can poll ConnectIp until it becomes unreachable, and then wait until it becomes reachable again
                            var autoDetected = await WaitForNextDeviceAsync(ConnectIp, token);
                            if (!autoDetected && !token.IsCancellationRequested)
                            {
                                var result = MessageBox.Show(
                                    $"تمت برمجة الجهاز بنجاح. يرجى فصله وتوصيل الجهاز الجديد (الافتراضي: {ConnectIp}) بالشبكة.\n\nاضغط 'موافق' لمواصلة برمجة الجهاز التالي ({TargetDevices[i + 1].IpAddress}).",
                                    "توصيل الجهاز التالي",
                                    MessageBoxButton.OKCancel,
                                    MessageBoxImage.Information);

                                if (result == MessageBoxResult.Cancel)
                                {
                                    _logger.Log("تم إلغاء عملية البرمجة الجماعية من قبل المستخدم.");
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        target.Status = TargetStatus.Failed;
                        target.ErrorMessage = ex.Message;
                        FailedCount++;

                        _logger.LogError($"[فشل] حدث خطأ في الجهاز {i + 1}: {ex.Message}");

                        if (i < TargetDevices.Count - 1 && !token.IsCancellationRequested)
                        {
                            var result = MessageBox.Show(
                                $"فشلت برمجة الجهاز الحالي:\n{ex.Message}\n\nهل تريد المتابعة لبرمجة الأجهزة المتبقية؟",
                                "فشل في برمجة جهاز",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (result == MessageBoxResult.No)
                            {
                                _logger.Log("تم إيقاف عملية البرمجة الجماعية بسبب فشل أحد الأجهزة.");
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                IsProgramming = false;
                ProgressPercent = 0;
                ProgressMessage = "";
                _programmingCts = null;
                _logger.Log($"[النهاية] اكتملت العملية. الأجهزة الناجحة: {CompletedCount} | الأجهزة الفاشلة: {FailedCount}");
            }
        }

        private async Task<bool> WaitForNextDeviceAsync(string ip, CancellationToken token)
        {
            var ping = new Ping();
            
            _logger.Log("جاري الكشف التلقائي: يرجى فصل الجهاز الحالي...");
            
            // 1. Wait for connect IP to become unreachable (disconnected)
            int unreachCount = 0;
            while (unreachCount < 3 && !token.IsCancellationRequested)
            {
                try
                {
                    var reply = await ping.SendPingAsync(ip, 1000);
                    if (reply.Status != IPStatus.Success)
                    {
                        unreachCount++;
                    }
                    else
                    {
                        unreachCount = 0;
                    }
                }
                catch
                {
                    unreachCount++;
                }
                await Task.Delay(1000, token);
            }

            if (token.IsCancellationRequested) return false;
            _logger.LogSuccess("تم اكتشاف فصل الجهاز السابق. الرجاء توصيل الجهاز التالي وتشغيله...");

            // 2. Wait for connect IP to become reachable again (new device booted)
            int reachCount = 0;
            while (reachCount < 3 && !token.IsCancellationRequested)
            {
                try
                {
                    var reply = await ping.SendPingAsync(ip, 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        reachCount++;
                    }
                    else
                    {
                        reachCount = 0;
                    }
                }
                catch
                {
                    reachCount = 0;
                }
                await Task.Delay(1000, token);
            }

            if (token.IsCancellationRequested) return false;
            
            // Give the uhttpd server an extra 3 seconds to fully initialize
            _logger.Log("تم اكتشاف اتصال جهاز جديد. في انتظار اكتمال بدء الخدمات...");
            await Task.Delay(4000, token);
            
            _logger.LogSuccess("تم الكشف عن جهاز جديد بنجاح! جاري المتابعة تلقائياً...");
            return true;
        }

        private void CancelProgramming()
        {
            if (IsProgramming && _programmingCts != null)
            {
                _programmingCts.Cancel();
                _logger.LogWarning("تم إرسال طلب إلغاء عملية البرمجة...");
            }
        }

        private async Task ExportLogsTxtAsync()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "ملف نصي (*.txt)|*.txt",
                FileName = $"Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await _logger.ExportToTxtAsync(saveFileDialog.FileName);
                _logger.LogSuccess($"تم تصدير السجلات النصية بنجاح إلى: {Path.GetFileName(saveFileDialog.FileName)}");
            }
        }

        private async Task ExportLogsJsonAsync()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "ملف جيسون (*.json)|*.json",
                FileName = $"Logs_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await _logger.ExportToJsonAsync(saveFileDialog.FileName);
                _logger.LogSuccess($"تم تصدير السجلات الهيكلية بنجاح إلى: {Path.GetFileName(saveFileDialog.FileName)}");
            }
        }

        public async Task LoadSavedNetworksListAsync()
        {
            var list = await _savedNetworksService.GetAllNetworksAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                SavedNetworks.Clear();
                foreach (var net in list)
                {
                    SavedNetworks.Add(net);
                }
            });
        }

        private async Task SaveNetworkProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(ProfileNameInput) || string.IsNullOrWhiteSpace(RemoteSsid))
            {
                MessageBox.Show("يرجى إدخال اسم ملف التعريف واسم الشبكة (SSID).", "مدخلات ناقصة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var net = new SavedNetwork
            {
                ProfileName = ProfileNameInput,
                Ssid = RemoteSsid,
                Password = RemotePassword,
                Notes = NotesInput
            };

            await _savedNetworksService.SaveNetworkAsync(net);
            await LoadSavedNetworksListAsync();
            _logger.LogSuccess($"تم حفظ الملف التعريفي للشبكة اللاسلكية بنجاح: {ProfileNameInput}");
        }

        private async Task DeleteNetworkProfileAsync()
        {
            if (SelectedSavedNetwork == null) return;
            
            var result = MessageBox.Show($"هل أنت متأكد من حذف الملف التعريفي '{SelectedSavedNetwork.ProfileName}'؟", "حذف ملف تعريف", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await _savedNetworksService.DeleteNetworkAsync(SelectedSavedNetwork.ProfileName);
                ProfileNameInput = "";
                NotesInput = "";
                await LoadSavedNetworksListAsync();
                _logger.LogSuccess("تم حذف ملف التعريف اللاسلكي.");
            }
        }

        private void LoadSelectedNetworkProfile()
        {
            if (SelectedSavedNetwork == null) return;
            RemoteSsid = SelectedSavedNetwork.Ssid;
            RemotePassword = SelectedSavedNetwork.Password;
            _logger.Log($"تم تحميل الإعدادات اللاسلكية من ملف التعريف: {SelectedSavedNetwork.ProfileName}");
        }

        private async Task LoadTemplatesListAsync()
        {
            var list = await _templateService.GetAllTemplatesAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                Templates.Clear();
                foreach (var temp in list)
                {
                    Templates.Add(temp);
                }
            });
        }

        private async Task SaveDeviceTemplateAsync()
        {
            if (string.IsNullOrWhiteSpace(TemplateNameInput))
            {
                MessageBox.Show("يرجى إدخال اسم القالب أولاً.", "اسم القالب مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var temp = new DeviceTemplate
            {
                TemplateName = TemplateNameInput,
                VlanId = VlanId,
                Gateway = Gateway,
                SubnetMask = SubnetMask,
                Mode = SelectedMode,
                Ssid24Ghz = Ssid24Ghz,
                Ssid5Ghz = Ssid5Ghz
            };

            await _templateService.SaveTemplateAsync(temp);
            await LoadTemplatesListAsync();
            _logger.LogSuccess($"تم حفظ القالب بنجاح: {TemplateNameInput}");
        }

        private async Task DeleteDeviceTemplateAsync()
        {
            if (SelectedTemplate == null) return;

            var result = MessageBox.Show($"هل أنت متأكد من حذف القالب '{SelectedTemplate.TemplateName}'؟", "حذف قالب", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await _templateService.DeleteTemplateAsync(SelectedTemplate.TemplateName);
                TemplateNameInput = "";
                await LoadTemplatesListAsync();
                _logger.LogSuccess("تم حذف القالب المختار.");
            }
        }

        private void LoadSelectedDeviceTemplate()
        {
            if (SelectedTemplate == null) return;

            VlanId = SelectedTemplate.VlanId;
            Gateway = SelectedTemplate.Gateway;
            SubnetMask = SelectedTemplate.SubnetMask;
            SelectedMode = SelectedTemplate.Mode;
            Ssid24Ghz = SelectedTemplate.Ssid24Ghz;
            Ssid5Ghz = SelectedTemplate.Ssid5Ghz;

            _logger.Log($"تم تحميل القالب: {SelectedTemplate.TemplateName}");
        }

        #endregion
        
        #region Settings Management
        
        private async Task LoadSettingsAsync()
        {
            var settings = await _appSettingsService.LoadAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConnectIp = settings.ConnectIp;
                ConnectUsername = settings.ConnectUsername;
                ConnectPassword = settings.ConnectPassword;
                
                BaseSsid24G = string.IsNullOrWhiteSpace(settings.BaseSsid24G) ? "LUX-4G" : settings.BaseSsid24G;
                BaseSsid5G = string.IsNullOrWhiteSpace(settings.BaseSsid5G) ? "LUX-5G" : settings.BaseSsid5G;
                BaseGateway = string.IsNullOrWhiteSpace(settings.BaseGateway) ? "10.0.0.1" : settings.BaseGateway;
                BaseSubnet = string.IsNullOrWhiteSpace(settings.BaseSubnet) ? "255.255.0.0" : settings.BaseSubnet;
                HostnamePrefix = string.IsNullOrWhiteSpace(settings.HostnamePrefix) ? "YAZ" : settings.HostnamePrefix;
                NetworkPrefix = string.IsNullOrWhiteSpace(settings.NetworkPrefix) ? "10.0.0" : settings.NetworkPrefix;
                StartingModemNumber = settings.StartingModemNumber == 0 ? 2 : settings.StartingModemNumber;
                LastProgrammedModemNumber = settings.LastProgrammedModemNumber;

                // Sync prefix to HostnameGenerator helper
                HostnameGenerator.Prefix = HostnamePrefix;

                WifiPassword = settings.WifiPassword;
                IsWifiEncrypted = settings.IsWifiEncrypted;
                RemoteSsid = settings.RemoteSsid;
                RemotePassword = settings.RemotePassword;

                NewPassword = settings.NewPassword;
                ChangePasswordAfterProgramming = settings.ChangePasswordAfterProgramming;
                TryNetworkPasswordFirst = settings.TryNetworkPasswordFirst;
                
                // Initialize Modem Number to next logical number starting from StartingModemNumber
                int nextVal = LastProgrammedModemNumber + 1;
                if (nextVal < StartingModemNumber)
                {
                    nextVal = StartingModemNumber;
                }
                while (nextVal == 255)
                {
                    nextVal++;
                }
                if (nextVal > 254)
                {
                    nextVal = StartingModemNumber;
                }
                ModemNumber = nextVal;

                if (Enum.TryParse<WirelessMode>(settings.SelectedMode, out var mode))
                {
                    SelectedMode = mode;
                }
                
                ValidateModemNumber();
            });
        }

        public async Task SaveSettingsAsync()
        {
            await Task.Run(() => SaveSettings());
        }

        public void SaveSettings()
        {
            // Sync prefix to HostnameGenerator helper
            HostnameGenerator.Prefix = HostnamePrefix;

            var settings = new AppSettings
            {
                ConnectIp = ConnectIp,
                ConnectUsername = ConnectUsername,
                ConnectPassword = ConnectPassword,
                
                BaseSsid24G = BaseSsid24G,
                BaseSsid5G = BaseSsid5G,
                BaseGateway = BaseGateway,
                BaseSubnet = BaseSubnet,
                HostnamePrefix = HostnamePrefix,
                NetworkPrefix = NetworkPrefix,
                StartingModemNumber = StartingModemNumber,
                LastProgrammedModemNumber = LastProgrammedModemNumber,

                WifiPassword = WifiPassword,
                IsWifiEncrypted = IsWifiEncrypted,
                SelectedMode = SelectedMode.ToString(),
                RemoteSsid = RemoteSsid,
                RemotePassword = RemotePassword,

                NewPassword = NewPassword,
                ChangePasswordAfterProgramming = ChangePasswordAfterProgramming,
                TryNetworkPasswordFirst = TryNetworkPasswordFirst
            };
            _appSettingsService.Save(settings);
        }
        
        #endregion
    }

    public class ScanNetworksViewModel : ObservableObject
    {
        private ObservableCollection<ScanResult> _networks;
        private ScanResult? _selectedNetwork;
        private string _searchText = "";

        public ScanNetworksViewModel(List<ScanResult> list)
        {
            AllNetworks = list;
            _networks = new ObservableCollection<ScanResult>(list.OrderByDescending(n => n.SignalStrength));
        }

        public List<ScanResult> AllNetworks { get; }

        public ObservableCollection<ScanResult> Networks
        {
            get => _networks;
            set => SetProperty(ref _networks, value);
        }

        public ScanResult? SelectedNetwork
        {
            get => _selectedNetwork;
            set => SetProperty(ref _selectedNetwork, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterNetworks();
                }
            }
        }

        private void FilterNetworks()
        {
            var filtered = AllNetworks.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(n => n.Ssid.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                                              n.Bssid.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }
            
            // Sort by signal strength descending
            filtered = filtered.OrderByDescending(n => n.SignalStrength);

            Networks = new ObservableCollection<ScanResult>(filtered);
        }
    }
}
