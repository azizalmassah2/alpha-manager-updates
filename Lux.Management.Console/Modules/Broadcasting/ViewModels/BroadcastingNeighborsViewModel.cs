using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core;
using MikroTikVoucherPrinter.Application.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Providers;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace Lux.Management.Console.Modules.Broadcasting.ViewModels
{
    public partial class BroadcastingNeighborsViewModel : ViewModelBase, IActivatable
    {
        private readonly IBroadcastingService _broadcastingService;
        private readonly IActiveRouterContext _activeRouterContext;
        private readonly IRouterOsProvider _routerOsProvider;
        private readonly IUserNotificationService _userNotificationService;
        private readonly CancellationTokenSource _cts = new();
        private readonly System.Collections.Generic.List<DiscoveredNetworkDevice> _allDevices = new();
        private readonly System.Windows.Threading.DispatcherTimer _debounceTimer;

        public event Action? RequestNavigateToMaintenance;

        [ObservableProperty]
        private ObservableCollection<DiscoveredNetworkDevice> _discoveredDevices = new();

        [ObservableProperty]
        private DiscoveredNetworkDevice? _selectedDevice;

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private string _scanStatus = "جاري التهيأة...";

        [ObservableProperty]
        private int _devicesCount;

        [ObservableProperty]
        private string _searchText = string.Empty;

        partial void OnSearchTextChanged(string value)
        {
            TriggerFilterUpdate();
        }

        private void TriggerFilterUpdate()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            });
        }

        private void ApplyFilter()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var query = SearchText?.Trim() ?? string.Empty;
                System.Collections.Generic.List<DiscoveredNetworkDevice> filtered;

                lock (_allDevices)
                {
                    // 1. تصفية الأجهزة عديمة البيانات (ماك ومنفذ فقط والبقية فارغ)
                    var validDevices = _allDevices.Where(d => 
                        !(string.IsNullOrEmpty(d.IpAddress) && 
                          string.IsNullOrEmpty(d.Hostname) && 
                          string.IsNullOrEmpty(d.Platform) && 
                          string.IsNullOrEmpty(d.Version))
                    ).ToList();

                    if (string.IsNullOrEmpty(query))
                    {
                        filtered = validDevices;
                    }
                    else
                    {
                        filtered = validDevices.Where(d =>
                            (d.IpAddress != null && d.IpAddress.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (d.MacAddress != null && d.MacAddress.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (d.Hostname != null && d.Hostname.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (d.Platform != null && d.Platform.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (d.Version != null && d.Version.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (d.BoardName != null && d.BoardName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (d.Interface != null && d.Interface.Contains(query, StringComparison.OrdinalIgnoreCase))
                        ).ToList();
                    }
                }

                // Incremental collection update to preserve selection/scroll
                var toRemove = DiscoveredDevices.Where(d => !filtered.Any(f => f.MacAddress.Equals(d.MacAddress, StringComparison.OrdinalIgnoreCase))).ToList();
                foreach (var r in toRemove)
                {
                    DiscoveredDevices.Remove(r);
                }

                foreach (var f in filtered)
                {
                    var existing = DiscoveredDevices.FirstOrDefault(d => d.MacAddress.Equals(f.MacAddress, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.IpAddress = f.IpAddress;
                        existing.Hostname = f.Hostname;
                        existing.Platform = f.Platform;
                        existing.Version = f.Version;
                        existing.Interface = f.Interface;
                        existing.Uptime = f.Uptime;
                        existing.BoardName = f.BoardName;
                        existing.IPv6 = f.IPv6;
                        existing.Age = f.Age;
                        existing.IsReachable = f.IsReachable;
                        existing.PingMs = f.PingMs;

                        int idx = DiscoveredDevices.IndexOf(existing);
                        if (idx >= 0)
                        {
                            DiscoveredDevices[idx] = existing;
                        }
                    }
                    else
                    {
                        DiscoveredDevices.Add(f);
                    }
                }

                DevicesCount = DiscoveredDevices.Count;
                ScanStatus = string.IsNullOrEmpty(query)
                    ? $"نشط — تم اكتشاف {DevicesCount} أجهزة ذكية"
                    : $"تصفية نشطة — تم العثور على {DevicesCount} أجهزة مطابقة";
            });
        }

        private async Task FetchDevicesAsync()
        {
            if (IsScanning) return;

            IsScanning = true;
            ScanStatus = "جاري كشف جيران الشبكة والبحث عن الأجهزة...";

            try
            {
                lock (_allDevices)
                {
                    _allDevices.Clear();
                }

                // 1. جلب الجيران من الراوتر (IP Neighbors) عبر MikroTik
                if (_activeRouterContext.IsConnected && _activeRouterContext.CurrentRouter != null)
                {
                    try
                    {
                        var result = await _routerOsProvider.ExecuteAsync(new MikroTikCommand { Command = "/ip/neighbor/print" });
                        if (result.IsSuccess && result.Value?.RawData != null)
                        {
                            lock (_allDevices)
                            {
                                foreach (var dict in result.Value.RawData)
                                {
                                    dict.TryGetValue("mac-address", out var mac);
                                    dict.TryGetValue("address", out var ip);
                                    dict.TryGetValue("identity", out var identity);
                                    dict.TryGetValue("platform", out var platform);
                                    dict.TryGetValue("version", out var version);
                                    dict.TryGetValue("board", out var board);
                                    dict.TryGetValue("interface", out var interfaceName);
                                    dict.TryGetValue("parent-interface", out var parentInterfaceName);
                                    dict.TryGetValue("uptime", out var uptime);
                                    dict.TryGetValue("age", out var age);

                                    if (string.IsNullOrEmpty(mac)) continue;

                                    string ipv6 = "no";
                                    if (dict.TryGetValue("ipv6", out var ipv6Str) && !string.IsNullOrEmpty(ipv6Str))
                                    {
                                        ipv6 = ipv6Str.ToLower() == "true" || ipv6Str == "yes" ? "yes" : "no";
                                    }
                                    else if (dict.TryGetValue("ipv6-address", out var ipv6Addr) && !string.IsNullOrEmpty(ipv6Addr))
                                    {
                                        ipv6 = "yes";
                                    }

                                    var decodedIdentity = DecodeArabicString(identity);
                                    var decodedPlatform = DecodeArabicString(platform);
                                    var decodedVersion = DecodeArabicString(version);
                                    var decodedBoard = DecodeArabicString(board);
                                    var decodedInterface = ResolveInterface(interfaceName, parentInterfaceName);

                                    var hostname = !string.IsNullOrEmpty(decodedIdentity) ? decodedIdentity : (!string.IsNullOrEmpty(decodedBoard) ? decodedBoard : string.Empty);
                                    var vendor = GetVendorFromPlatform(mac, decodedPlatform, hostname);

                                    var device = new DiscoveredNetworkDevice
                                    {
                                        MacAddress = mac,
                                        IpAddress = ip ?? string.Empty,
                                        Hostname = hostname,
                                        Platform = decodedPlatform,
                                        Version = decodedVersion,
                                        Interface = decodedInterface,
                                        Uptime = uptime ?? string.Empty,
                                        BoardName = decodedBoard,
                                        IPv6 = ipv6,
                                        Age = age ?? "0",
                                        Protocol = "Router Neighbor",
                                        Vendor = vendor,
                                        IsReachable = true
                                    };

                                    var existing = _allDevices.FirstOrDefault(d => d.MacAddress.Equals(device.MacAddress, StringComparison.OrdinalIgnoreCase));
                                    if (existing != null)
                                    {
                                        if (string.IsNullOrEmpty(existing.Hostname) || existing.Hostname == existing.IpAddress)
                                            existing.Hostname = device.Hostname;
                                        if (string.IsNullOrEmpty(existing.Platform))
                                            existing.Platform = device.Platform;
                                        if (string.IsNullOrEmpty(existing.Version))
                                            existing.Version = device.Version;

                                        if (!string.IsNullOrEmpty(device.Interface) && device.Interface != "غير محدد")
                                        {
                                            if (string.IsNullOrEmpty(existing.Interface) || existing.Interface == "غير محدد")
                                                existing.Interface = device.Interface;
                                        }

                                        if (string.IsNullOrEmpty(existing.Uptime))
                                            existing.Uptime = device.Uptime;
                                        if (string.IsNullOrEmpty(existing.BoardName))
                                            existing.BoardName = device.BoardName;
                                        if (string.IsNullOrEmpty(existing.IpAddress))
                                            existing.IpAddress = device.IpAddress;

                                        existing.IPv6 = device.IPv6;
                                        existing.Age = device.Age;
                                        existing.IsReachable = true;
                                    }
                                    else
                                    {
                                        _allDevices.Add(device);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[⚠️] Failed to fetch router neighbors: {ex.Message}");
                    }
                }

                // 2. فحص الأجهزة المحلية عبر الشبكة (ScanLocalNetworkAsync)
                try
                {
                    var localDevices = await _broadcastingService.ScanLocalNetworkAsync(null, _cts.Token);
                    if (localDevices != null)
                    {
                        lock (_allDevices)
                        {
                            foreach (var dev in localDevices)
                            {
                                var decodedHostname = DecodeArabicString(dev.Hostname);
                                var decodedPlatform = DecodeArabicString(dev.Platform);
                                var decodedVersion = DecodeArabicString(dev.Version);
                                var decodedInterface = ResolveInterface(dev.Interface);

                                var existing = _allDevices.FirstOrDefault(d => d.MacAddress.Equals(dev.MacAddress, StringComparison.OrdinalIgnoreCase));
                                if (existing != null)
                                {
                                    if (!string.IsNullOrEmpty(decodedHostname) && (string.IsNullOrEmpty(existing.Hostname) || existing.Hostname == existing.IpAddress))
                                        existing.Hostname = decodedHostname;
                                    if (!string.IsNullOrEmpty(decodedPlatform) && string.IsNullOrEmpty(existing.Platform))
                                        existing.Platform = decodedPlatform;
                                    if (!string.IsNullOrEmpty(decodedVersion) && string.IsNullOrEmpty(existing.Version))
                                        existing.Version = decodedVersion;

                                    if (!string.IsNullOrEmpty(decodedInterface) && decodedInterface != "غير محدد")
                                    {
                                        if (string.IsNullOrEmpty(existing.Interface) || existing.Interface == "غير محدد")
                                            existing.Interface = decodedInterface;
                                    }

                                    if (string.IsNullOrEmpty(existing.IpAddress))
                                        existing.IpAddress = dev.IpAddress;

                                    existing.IsReachable = true;
                                }
                                else
                                {
                                    dev.Hostname = decodedHostname;
                                    dev.Platform = decodedPlatform;
                                    dev.Version = decodedVersion;
                                    dev.Interface = decodedInterface;
                                    _allDevices.Add(dev);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[⚠️] Failed to scan local network: {ex.Message}");
                }

                // 3. تصفية وتحديث القائمة
                ApplyFilter();
            }
            finally
            {
                IsScanning = false;
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ScanStatus = $"مكتمل — تم العثور على {DiscoveredDevices.Count} أجهزة";
                });
            }
        }

        private string CleanInterfaceName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return "غير محدد";

            // فحص وجود أقواس وسحب الجزء الفيزيائي (مثل ether2)
            int openParen = name.IndexOf('(');
            int closeParen = name.IndexOf(')');
            if (openParen >= 0 && closeParen > openParen)
            {
                var outer = name.Substring(0, openParen).Trim();
                var inner = name.Substring(openParen + 1, closeParen - openParen - 1).Trim();

                if (IsPhysicalPort(inner) && !IsPhysicalPort(outer))
                {
                    return inner;
                }
                if (IsPhysicalPort(outer) && !IsPhysicalPort(inner))
                {
                    return outer;
                }

                bool outerIsBridge = outer.Contains("bridge", StringComparison.OrdinalIgnoreCase);
                bool innerIsBridge = inner.Contains("bridge", StringComparison.OrdinalIgnoreCase);
                if (outerIsBridge && !innerIsBridge && !string.IsNullOrEmpty(inner))
                {
                    return inner;
                }
                if (innerIsBridge && !outerIsBridge && !string.IsNullOrEmpty(outer))
                {
                    return outer;
                }

                return !string.IsNullOrEmpty(inner) ? inner : outer;
            }

            name = name.Trim();
            return string.IsNullOrEmpty(name) ? "غير محدد" : name;
        }

        private bool IsPhysicalPort(string port)
        {
            if (string.IsNullOrEmpty(port)) return false;
            
            var p = port.ToLowerInvariant();
            return p.StartsWith("ether") || p.StartsWith("wlan") || p.StartsWith("sfp") || 
                   p.StartsWith("wifi") || p.StartsWith("ath") || p.StartsWith("wds") || 
                   p.StartsWith("combo") || p.StartsWith("sfpplus") || p.StartsWith("lan");
        }

        private string ResolveInterface(string? interfaceName, string? parentInterface = null)
        {
            if (!string.IsNullOrEmpty(parentInterface) && IsPhysicalPort(parentInterface))
            {
                return CleanInterfaceName(parentInterface);
            }
            if (!string.IsNullOrEmpty(interfaceName))
            {
                return CleanInterfaceName(interfaceName);
            }
            return "غير محدد";
        }

        private string GetVendorFromPlatform(string mac, string platform, string hostname)
        {
            if (string.IsNullOrEmpty(mac)) return "غير معروف";

            var cleanMac = mac.Replace("-", ":").ToUpperInvariant();
            if (cleanMac.Length >= 8)
            {
                var oui = cleanMac.Substring(0, 8);
                if (oui.StartsWith("00:15:6D") || oui.StartsWith("00:27:22") || oui.StartsWith("0C:80:63") ||
                    oui.StartsWith("80:2A:A8") || oui.StartsWith("B4:FB:E4") || oui.StartsWith("E0:63:DA") ||
                    oui.StartsWith("DC:9F:DB") || oui.StartsWith("F0:9F:C2") || oui.StartsWith("78:8A:20"))
                    return "Ubiquiti";

                if (oui.StartsWith("08:55:31") || oui.StartsWith("18:FD:74") || oui.StartsWith("2C:C8:1B") ||
                    oui.StartsWith("48:8F:5A") || oui.StartsWith("6C:3B:6B") || oui.StartsWith("D4:CA:6D") ||
                    oui.StartsWith("E4:8D:8C"))
                    return "MikroTik";
            }

            var lowerP = platform.ToLowerInvariant();
            var lowerH = hostname.ToLowerInvariant();

            if (lowerP.Contains("mikrotik") || lowerH.Contains("mikrotik"))
                return "MikroTik";
            if (lowerP.Contains("ubiquiti") || lowerP.Contains("unifi") || lowerH.Contains("ubnt") || lowerH.Contains("emlak") || lowerH.Contains("ap"))
                return "Ubiquiti";
            if (lowerP.Contains("linux") || lowerP.Contains("openwrt"))
                return "Linux/OpenWrt";

            return "غير معروف";
        }

        private string DecodeArabicString(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            try
            {
                if (ContainsArabic(value)) return value;

                byte[] bytes = System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(value);
                var win1256 = System.Text.Encoding.GetEncoding("windows-1256");
                string decoded = win1256.GetString(bytes);

                if (ContainsArabic(decoded))
                {
                    return decoded;
                }

                string utf8Decoded = System.Text.Encoding.UTF8.GetString(bytes);
                if (ContainsArabic(utf8Decoded))
                {
                    return utf8Decoded;
                }
            }
            catch { }

            return value;
        }

        private bool ContainsArabic(string text)
        {
            return text.Any(c => c >= 0x0600 && c <= 0x06FF);
        }

        [RelayCommand]
        private async Task PingDeviceAsync(DiscoveredNetworkDevice? device)
        {
            if (device == null) return;
            try
            {
                var ping = new Ping();
                var reply = await ping.SendPingAsync(device.IpAddress, 1000);
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    device.IsReachable = reply.Status == IPStatus.Success;
                    device.PingMs = reply.RoundtripTime;

                    var index = DiscoveredDevices.IndexOf(device);
                    if (index >= 0)
                    {
                        DiscoveredDevices[index] = device;
                    }

                    ScanStatus = reply.Status == IPStatus.Success
                        ? $"✅ {device.IpAddress} — {reply.RoundtripTime} ms"
                        : $"❌ {device.IpAddress} — لا يستجيب";
                });
            }
            catch (Exception ex)
            {
                ScanStatus = $"خطأ في Ping: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CopyIp(DiscoveredNetworkDevice? device)
        {
            if (device == null || string.IsNullOrEmpty(device.IpAddress)) return;
            try
            {
                System.Windows.Clipboard.SetText(device.IpAddress);
                ScanStatus = $"تم نسخ عنوان IP: {device.IpAddress}";
            }
            catch { }
        }

        [RelayCommand]
        private void CopyMac(DiscoveredNetworkDevice? device)
        {
            if (device == null || string.IsNullOrEmpty(device.MacAddress)) return;
            try
            {
                System.Windows.Clipboard.SetText(device.MacAddress);
                ScanStatus = $"تم نسخ عنوان MAC: {device.MacAddress}";
            }
            catch { }
        }

        [RelayCommand]
        private async Task RefreshDiscoveryAsync()
        {
            await FetchDevicesAsync();
        }

        [RelayCommand]
        private async Task RegisterDeviceAsync(DiscoveredNetworkDevice? device)
        {
            if (device == null) return;
            try
            {
                var routerId = _activeRouterContext.CurrentRouter?.Id ?? Guid.Empty;
                
                var newDevice = new BroadcastingDevice
                {
                    DisplayName = !string.IsNullOrEmpty(device.Hostname) ? device.Hostname : device.MacAddress,
                    IpAddress = device.IpAddress,
                    MacAddress = device.MacAddress,
                    DeviceType = "Modem",
                    Vendor = device.Vendor,
                    RouterId = routerId,
                    Notes = $"تم تسجيله تلقائياً من الجيران (المنفذ: {device.Interface})"
                };

                await _broadcastingService.AddDeviceAsync(newDevice);
                ScanStatus = $"✅ تم حفظ الجهاز {newDevice.DisplayName} في المنظومة بنجاح!";
                
                // إظهار إشعار نجاح العملية للمستخدم
                _userNotificationService.ShowSuccess($"تم حفظ الجهاز {newDevice.DisplayName} بنجاح!", "حفظ الجهاز");

                // الانتقال تلقائياً إلى شاشة الإعداد والصيانة
                RequestNavigateToMaintenance?.Invoke();
            }
            catch (Exception ex)
            {
                ScanStatus = $"❌ فشل حفظ الجهاز: {ex.Message}";
                _userNotificationService.ShowError($"فشل حفظ الجهاز: {ex.Message}", "حفظ الجهاز");
            }
        }

        public BroadcastingNeighborsViewModel(
            IPermissionService permissionService,
            IEventBus eventBus,
            IBroadcastingService broadcastingService,
            IActiveRouterContext activeRouterContext,
            IRouterOsProvider routerOsProvider,
            IUserNotificationService userNotificationService)
            : base(permissionService, eventBus)
        {
            _broadcastingService = broadcastingService;
            _activeRouterContext = activeRouterContext;
            _routerOsProvider = routerOsProvider;
            _userNotificationService = userNotificationService;

            _debounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ApplyFilter();
            };
        }

        public async Task ActivateAsync()
        {
            await FetchDevicesAsync();
        }

        public override void Dispose()
        {
            _debounceTimer.Stop();
            _cts.Cancel();
            base.Dispose();
        }
    }
}
