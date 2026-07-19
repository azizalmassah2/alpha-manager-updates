using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;
using Lux.MikroTik.Models;
using Microsoft.Extensions.Logging;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Views;
using Microsoft.Extensions.DependencyInjection;
using Lux.Platform.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public enum VlanHealthStatus
{
    Healthy,
    Busy,
    Warning,
    Congested,
    Offline
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public partial class VlanMonitorItem : ObservableObject
{
    public string VlanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _localName = string.Empty;

    public string DisplayName => string.IsNullOrEmpty(LocalName) ? Name : LocalName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusButtonText))]
    private bool _isDisabled;

    public string StatusButtonText => IsDisabled ? "تفعيل" : "تعطيل";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectedClientsText))]
    private int _connectedClients;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeakClientsText))]
    private int _peakClients;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadSpeedText))]
    private double _downloadSpeedMbps;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeakDownloadSpeedText))]
    private double _peakDownloadSpeedMbps;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UploadSpeedText))]
    private double _uploadSpeedMbps;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeakUploadSpeedText))]
    private double _peakUploadSpeedMbps;

    [ObservableProperty]
    private double _capacityMbps = 50.0; // Default capacity: 50 Mbps

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UtilizationText))]
    [NotifyPropertyChangedFor(nameof(LoadBlocks))]
    private double _utilizationPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadBlocks))]
    private VlanHealthStatus _healthStatus = VlanHealthStatus.Offline;

    [ObservableProperty]
    private DateTime _lastUpdated = DateTime.MinValue;

    public string DownloadSpeedText => LastByteTime == DateTime.MinValue ? "--" : $"{DownloadSpeedMbps:F2} Mbps";
    public string UploadSpeedText => LastByteTime == DateTime.MinValue ? "--" : $"{UploadSpeedMbps:F2} Mbps";
    public string UtilizationText => LastByteTime == DateTime.MinValue ? "--" : $"{UtilizationPercent:F1}%";
    public string ConnectedClientsText => LastByteTime == DateTime.MinValue ? "--" : $"{ConnectedClients}";

    public string PeakDownloadSpeedText => PeakDownloadSpeedMbps == 0 ? "--" : $"{PeakDownloadSpeedMbps:F2} Mbps";
    public string PeakUploadSpeedText => PeakUploadSpeedMbps == 0 ? "--" : $"{PeakUploadSpeedMbps:F2} Mbps";
    public string PeakClientsText => PeakClients == 0 ? "--" : $"{PeakClients}";

    public string LoadBlocks
    {
        get
        {
            if (LastByteTime == DateTime.MinValue || HealthStatus == VlanHealthStatus.Offline)
                return "░░░░░░░░░░";
            int filled = (int)Math.Round(UtilizationPercent / 10.0);
            if (filled < 0) filled = 0;
            if (filled > 10) filled = 10;
            return new string('█', filled) + new string('░', 10 - filled);
        }
    }

    public string TotalDownloadText => LastByteTime == DateTime.MinValue ? "--" : FormatBytes(LastRxBytes);
    public string TotalUploadText => LastByteTime == DateTime.MinValue ? "--" : FormatBytes(LastTxBytes);

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        double tb = 1099511627776.0;
        double gb = 1073741824.0;
        double mb = 1048576.0;
        double kb = 1024.0;

        if (bytes >= tb)
            return $"{bytes / tb:F2} TB";
        if (bytes >= gb)
            return $"{bytes / gb:F2} GB";
        if (bytes >= mb)
            return $"{bytes / mb:F2} MB";
        if (bytes >= kb)
            return $"{bytes / kb:F2} KB";
        
        return $"{bytes} B";
    }

    public long LastRxBytes { get; set; }
    public long LastTxBytes { get; set; }

    private DateTime _lastByteTime = DateTime.MinValue;
    public DateTime LastByteTime
    {
        get => _lastByteTime;
        set
        {
            if (SetProperty(ref _lastByteTime, value))
            {
                OnPropertyChanged(nameof(DownloadSpeedText));
                OnPropertyChanged(nameof(UploadSpeedText));
                OnPropertyChanged(nameof(UtilizationText));
                OnPropertyChanged(nameof(ConnectedClientsText));
                OnPropertyChanged(nameof(LoadBlocks));
                OnPropertyChanged(nameof(TotalDownloadText));
                OnPropertyChanged(nameof(TotalUploadText));
            }
        }
    }

    [ObservableProperty]
    private string _deviceIp = string.Empty;

    [ObservableProperty]
    private string _deviceStatus = "Offline"; // "Healthy", "Warning", "Offline"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LatencyText))]
    private double _latencyMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastSeenText))]
    private DateTime? _lastSeen;

    public string LatencyText => DeviceStatus == "Offline" || LatencyMs == 0 ? "--" : $"{LatencyMs:F1} ms";
    public string LastSeenText => LastSeen.HasValue ? LastSeen.Value.ToString("yyyy-MM-dd HH:mm:ss") : "--";
}

public partial class AlertItem : ObservableObject
{
    public DateTime Timestamp { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
}

public partial class NocViewModel : ObservableObject, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterManagementService _routerService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<NocViewModel> _logger;

    private Timer? _throughputTimer;
    private Timer? _clientsCountTimer;
    private Timer? _systemResourcesTimer;
    private CancellationTokenSource? _cts;

    // Interface name -> IP subnet (e.g. "VLAN-12" -> "192.168.12.1/24")
    private readonly Dictionary<string, string> _interfaceSubnets = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private ObservableCollection<VlanMonitorItem> _vlans = new();

    [ObservableProperty]
    private ObservableCollection<AlertItem> _alerts = new();

    [ObservableProperty]
    private VlanMonitorItem? _selectedVlan;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // ── KPI Metrics ──
    [ObservableProperty]
    private int _totalClients;

    [ObservableProperty]
    private int _activeVlansCount;

    [ObservableProperty]
    private double _totalDownloadMbps;

    [ObservableProperty]
    private double _totalUploadMbps;

    [ObservableProperty]
    private string _highestUsageVlanName = "—";

    [ObservableProperty]
    private int _congestedVlansCount;

    [ObservableProperty]
    private int _offlineVlansCount;

    [ObservableProperty]
    private int _nocMonitoringInterval;

    partial void OnNocMonitoringIntervalChanged(int value)
    {
        if (value < 1) value = 1;
        _settingsService.Set("NocMonitoringInterval", value);
        _ = _settingsService.SaveAsync();
    }

    // ── CPU / RAM Metrics ──
    [ObservableProperty]
    private double _cpuUsagePercent;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus _eventBus;
    private readonly IDevicePingService _devicePingService;

    [ObservableProperty]
    private double _freeMemoryMb;

    [ObservableProperty]
    private double _totalMemoryMb;

    public NocViewModel(
        IActiveRouterContext activeRouterContext,
        IRouterManagementService routerService,
        ISettingsService settingsService,
        IServiceScopeFactory scopeFactory,
        IEventBus eventBus,
        IDevicePingService devicePingService,
        ILogger<NocViewModel> logger)
    {
        _activeRouterContext = activeRouterContext;
        _routerService = routerService;
        _settingsService = settingsService;
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
        _devicePingService = devicePingService;
        _logger = logger;

        _nocMonitoringInterval = _settingsService.Get("NocMonitoringInterval", 100);

        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        
        // Subscribe to background health monitor events
        _eventBus.Subscribe<MikroTikVoucherPrinter.Infrastructure.Monitoring.VlanHealthChangedEvent>(this, OnVlanHealthChanged);

        _ = LoadInitialDataAndStartAsync();
    }

    private void OnVlanHealthChanged(MikroTikVoucherPrinter.Infrastructure.Monitoring.VlanHealthChangedEvent ev)
    {
        if (_activeRouterContext.CurrentRouter == null || ev.RouterId != _activeRouterContext.CurrentRouter.Id)
            return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var vlan = Vlans.FirstOrDefault(v => v.VlanId == ev.VlanId);
            if (vlan != null)
            {
                vlan.DeviceIp = ev.DeviceIp;
                vlan.DeviceStatus = ev.Status;
                vlan.LatencyMs = ev.LatencyMs;
                vlan.LastSeen = ev.LastSeen;
            }
        });
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        _ = LoadInitialDataAndStartAsync();
    }

    [RelayCommand]
    private async Task LoadInitialDataAndStartAsync()
    {
        StopPolling();

        if (!_activeRouterContext.IsConnected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Vlans.Clear();
                Alerts.Clear();
                SelectedVlan = null;
                ResetMetrics();
            });
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        _cts = new CancellationTokenSource();

        try
        {
            _logger.LogInformation("🚀 [NOC] Initializing monitoring configurations...");
            
            // 1. Fetch Interface Subnets
            _interfaceSubnets.Clear();
            var addrResponse = await _routerService.ExecuteQueryAsync("/ip/address/print", _cts.Token);
            foreach (var row in addrResponse.RawData)
            {
                var address = row.GetValueOrDefault("address", "");
                var iface = row.GetValueOrDefault("interface", "");
                if (!string.IsNullOrEmpty(address) && !string.IsNullOrEmpty(iface))
                {
                    _interfaceSubnets[iface] = address;
                }
            }

            // 2. Fetch VLAN Interfaces
            var vlanResponse = await _routerService.ExecuteQueryAsync("/interface/vlan/print", _cts.Token);
            var initialVlans = new List<VlanMonitorItem>();
            var localNames = _settingsService.Get<Dictionary<string, string>>("LocalVlanNames") ?? new Dictionary<string, string>();
            
            var currentRouterId = _activeRouterContext.CurrentRouter?.Id ?? Guid.Empty;
            List<MikroTikVoucherPrinter.Domain.Entities.Platform.VlanMonitoringConfig> monitorConfigs = new();
            if (currentRouterId != Guid.Empty)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                monitorConfigs = await db.VlanMonitoringConfigs
                    .Where(c => c.RouterId == currentRouterId)
                    .ToListAsync(_cts.Token);
            }

            // إضافة الفيلانات
            foreach (var row in vlanResponse.RawData)
            {
                var vlanId = row.GetValueOrDefault("vlan-id", "");
                var name = row.GetValueOrDefault("name", "");
                localNames.TryGetValue(vlanId, out var localName);

                var monitorConfig = monitorConfigs.FirstOrDefault(c => c.VlanId == vlanId);
                var deviceIp = monitorConfig?.DeviceIp ?? string.Empty;
                var enableMonitoring = monitorConfig?.EnableMonitoring ?? false;

                initialVlans.Add(new VlanMonitorItem
                {
                    VlanId = vlanId,
                    Name = name,
                    LocalName = localName ?? string.Empty,
                    CapacityMbps = 50.0,
                    HealthStatus = VlanHealthStatus.Offline,
                    LastUpdated = DateTime.Now,
                    DeviceIp = deviceIp,
                    DeviceStatus = enableMonitoring ? "Offline" : "NotMonitored"
                });
            }

            // إضافة البريدج (Bridges)
            try
            {
                var bridgeResponse = await _routerService.ExecuteQueryAsync("/interface/bridge/print", _cts.Token);
                foreach (var row in bridgeResponse.RawData)
                {
                    var name = row.GetValueOrDefault("name", "");
                    if (string.IsNullOrEmpty(name)) continue;

                    // لتجنب التكرار
                    if (initialVlans.Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;

                    var vlanId = "bridge_" + name;
                    localNames.TryGetValue(vlanId, out var localName);

                    var monitorConfig = monitorConfigs.FirstOrDefault(c => c.VlanId == vlanId);
                    var deviceIp = monitorConfig?.DeviceIp ?? string.Empty;
                    var enableMonitoring = monitorConfig?.EnableMonitoring ?? false;

                    initialVlans.Add(new VlanMonitorItem
                    {
                        VlanId = vlanId,
                        Name = name,
                        LocalName = string.IsNullOrEmpty(localName) ? $"{name} (Bridge)" : localName,
                        CapacityMbps = 100.0,
                        HealthStatus = VlanHealthStatus.Offline,
                        LastUpdated = DateTime.Now,
                        DeviceIp = deviceIp,
                        DeviceStatus = enableMonitoring ? "Offline" : "NotMonitored"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ [NOC] Failed to query bridge interfaces: {Message}", ex.Message);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Vlans.Clear();
                foreach (var vlan in initialVlans)
                {
                    Vlans.Add(vlan);
                }
            });

            // Start Polling Engine
            StartPolling();
            _logger.LogInformation("✅ [NOC] Polling loops started successfully.");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to initialize NOC: {ex.Message}";
            _logger.LogError(ex, "🚫 [NOC Init Error] Failed to initialize NOC monitor.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void StartPolling()
    {
        _throughputTimer = new Timer(async _ => await PollThroughputAsync(), null, 0, 3000);
        _clientsCountTimer = new Timer(async _ => await PollClientsCountAsync(), null, 1000, 10000);
        _systemResourcesTimer = new Timer(async _ => await PollSystemResourcesAsync(), null, 2000, 30000);
        if (_cts != null)
        {
            _ = Task.Run(async () => await DevicePingLoopAsync(_cts.Token), _cts.Token);
        }
    }

    private void StopPolling()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _throughputTimer?.Dispose();
        _throughputTimer = null;

        _clientsCountTimer?.Dispose();
        _clientsCountTimer = null;

        _systemResourcesTimer?.Dispose();
        _systemResourcesTimer = null;
    }

    private async Task DevicePingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var interval = NocMonitoringInterval;
                if (interval < 1) interval = 10;
                
                await Task.Delay(TimeSpan.FromSeconds(interval), token);
                
                List<VlanMonitorItem> vlansToPing;
                lock (Vlans)
                {
                    vlansToPing = Vlans.Where(v => !string.IsNullOrEmpty(v.DeviceIp) && v.DeviceStatus != "NotMonitored").ToList();
                }

                if (vlansToPing.Count == 0) continue;
                
                var tasks = vlansToPing.Select(async vlan =>
                {
                    var (isReachable, latencyMs) = await _devicePingService.PingAsync(vlan.DeviceIp, token);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (isReachable)
                        {
                            vlan.DeviceStatus = "Healthy";
                            vlan.LatencyMs = latencyMs;
                            vlan.LastSeen = DateTime.Now;
                        }
                        else
                        {
                            vlan.DeviceStatus = "Offline";
                            vlan.LatencyMs = -1;
                        }
                    });
                });
                
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [NOC Ping Loop] Failed during device pings cycle");
            }
        }
    }

    // ── Timer Loop 1: Throughput and Health Calculation (Every 3 seconds) ──
    private async Task PollThroughputAsync()
    {
        if (_cts == null || _cts.IsCancellationRequested) return;

        try
        {
            var res = await _routerService.ExecuteQueryAsync("/interface/print", _cts.Token);
            var now = DateTime.Now;

            Application.Current.Dispatcher.Invoke(() =>
            {
                double totalDl = 0;
                double totalUl = 0;
                VlanMonitorItem? maxVlan = null;
                int congestedCount = 0;
                int offlineCount = 0;

                foreach (var vlan in Vlans)
                {
                    var row = res.RawData.FirstOrDefault(d => string.Equals(d.GetValueOrDefault("name", ""), vlan.Name, StringComparison.OrdinalIgnoreCase));
                    if (row != null)
                    {
                        var isRunning = row.GetValueOrDefault("running", "false") == "true";
                        var isDisabled = row.GetValueOrDefault("disabled", "false") == "true";
                        vlan.IsDisabled = isDisabled;
                        var rxBytes = long.TryParse(row.GetValueOrDefault("rx-byte", "0"), out var rx) ? rx : 0;
                        var txBytes = long.TryParse(row.GetValueOrDefault("tx-byte", "0"), out var tx) ? tx : 0;

                        if (isDisabled)
                        {
                            vlan.HealthStatus = VlanHealthStatus.Offline;
                            vlan.DownloadSpeedMbps = 0;
                            vlan.UploadSpeedMbps = 0;
                            vlan.UtilizationPercent = 0;
                            offlineCount++;
                        }
                        else
                        {
                            if (vlan.LastByteTime != DateTime.MinValue)
                            {
                                var seconds = (now - vlan.LastByteTime).TotalSeconds;
                                if (seconds > 0)
                                {
                                    // 8 bits per byte, 1,000,000 bits per Megabit
                                    vlan.DownloadSpeedMbps = Math.Round(((rxBytes - vlan.LastRxBytes) * 8.0) / (seconds * 1000000.0), 2);
                                    vlan.UploadSpeedMbps = Math.Round(((txBytes - vlan.LastTxBytes) * 8.0) / (seconds * 1000000.0), 2);
                                    
                                    if (vlan.DownloadSpeedMbps < 0) vlan.DownloadSpeedMbps = 0;
                                    if (vlan.UploadSpeedMbps < 0) vlan.UploadSpeedMbps = 0;

                                    vlan.UtilizationPercent = Math.Round((vlan.DownloadSpeedMbps / vlan.CapacityMbps) * 100.0, 1);
                                    if (vlan.UtilizationPercent > 100.0) vlan.UtilizationPercent = 100.0;
                                }
                            }

                            vlan.LastRxBytes = rxBytes;
                            vlan.LastTxBytes = txBytes;
                            vlan.LastByteTime = now;
                            vlan.LastUpdated = now;

                            // Update peaks
                            if (vlan.DownloadSpeedMbps > vlan.PeakDownloadSpeedMbps)
                                vlan.PeakDownloadSpeedMbps = vlan.DownloadSpeedMbps;
                            if (vlan.UploadSpeedMbps > vlan.PeakUploadSpeedMbps)
                                vlan.PeakUploadSpeedMbps = vlan.UploadSpeedMbps;

                            // Calculate Health
                            if (!isRunning)
                            {
                                vlan.HealthStatus = VlanHealthStatus.Offline;
                                offlineCount++;
                            }
                            else if (vlan.UtilizationPercent >= 95.0)
                            {
                                vlan.HealthStatus = VlanHealthStatus.Congested;
                                congestedCount++;
                                AddAlert(AlertSeverity.Critical, $"🚨 [حرِج جداً] تجاوز استهلاك {vlan.Name} معدل {vlan.UtilizationPercent}% ({vlan.DownloadSpeedMbps} Mbps)");
                            }
                            else if (vlan.UtilizationPercent >= 80.0)
                            {
                                vlan.HealthStatus = VlanHealthStatus.Warning;
                                AddAlert(AlertSeverity.Warning, $"⚠️ [حمل مرتفع] تجاوز استهلاك {vlan.Name} معدل {vlan.UtilizationPercent}% ({vlan.DownloadSpeedMbps} Mbps)");
                            }
                            else if (vlan.UtilizationPercent >= 50.0)
                            {
                                vlan.HealthStatus = VlanHealthStatus.Busy;
                            }
                            else
                            {
                                vlan.HealthStatus = VlanHealthStatus.Healthy;
                            }
                        }

                        totalDl += vlan.DownloadSpeedMbps;
                        totalUl += vlan.UploadSpeedMbps;

                        long totalUsage = vlan.LastRxBytes + vlan.LastTxBytes;
                        long maxUsage = maxVlan != null ? (maxVlan.LastRxBytes + maxVlan.LastTxBytes) : -1;
                        if (maxVlan == null || totalUsage > maxUsage)
                        {
                            maxVlan = vlan;
                        }
                    }
                }

                TotalDownloadMbps = Math.Round(totalDl, 1);
                TotalUploadMbps = Math.Round(totalUl, 1);
                ActiveVlansCount = Vlans.Count(v => v.HealthStatus != VlanHealthStatus.Offline);
                CongestedVlansCount = congestedCount;
                OfflineVlansCount = offlineCount;

                if (maxVlan != null && (maxVlan.LastRxBytes + maxVlan.LastTxBytes) > 0)
                {
                    long totalUsage = maxVlan.LastRxBytes + maxVlan.LastTxBytes;
                    HighestUsageVlanName = $"{maxVlan.Name} ({VlanMonitorItem.FormatBytes(totalUsage)})";
                }
                else
                {
                    HighestUsageVlanName = "—";
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ [NOC Polling Error] Failed to update interface speeds: {Message}", ex.Message);
        }
    }

    // ── Timer Loop 2: Connected Clients Count & Mapping (Every 10 seconds) ──
    private async Task PollClientsCountAsync()
    {
        if (_cts == null || _cts.IsCancellationRequested) return;

        try
        {
            var clientIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Hotspot active
            try
            {
                var hs = await _routerService.ExecuteQueryAsync("/ip/hotspot/active/print", _cts.Token);
                foreach (var row in hs.RawData)
                {
                    var ip = row.GetValueOrDefault("address", "");
                    if (!string.IsNullOrEmpty(ip)) clientIps.Add(ip);
                }
            }
            catch { /* skip if not running */ }

            // 2. PPPoE active
            try
            {
                var ppp = await _routerService.ExecuteQueryAsync("/interface/ppp-active/print", _cts.Token);
                foreach (var row in ppp.RawData)
                {
                    var ip = row.GetValueOrDefault("address", "");
                    if (!string.IsNullOrEmpty(ip)) clientIps.Add(ip);
                }
            }
            catch { /* skip if not running */ }



            // Map IP addresses to VLAN interfaces based on subnet mapping
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vlanClientCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var ip in clientIps)
                {
                    // Find matching VLAN interface
                    foreach (var kvp in _interfaceSubnets)
                    {
                        if (IsIpInSubnet(ip, kvp.Value))
                        {
                            vlanClientCounts[kvp.Key] = vlanClientCounts.GetValueOrDefault(kvp.Key, 0) + 1;
                            break; // matched
                        }
                    }
                }

                foreach (var vlan in Vlans)
                {
                    var count = vlanClientCounts.GetValueOrDefault(vlan.Name, 0);
                    vlan.ConnectedClients = count;
                    if (count > vlan.PeakClients)
                        vlan.PeakClients = count;
                }

                TotalClients = clientIps.Count;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ [NOC Polling Error] Failed to update client counts: {Message}", ex.Message);
        }
    }

    // ── Timer Loop 3: System Resources (Every 30 seconds) ──
    private async Task PollSystemResourcesAsync()
    {
        if (_cts == null || _cts.IsCancellationRequested) return;

        try
        {
            var res = await _routerService.ExecuteQueryAsync("/system/resource/print", _cts.Token);
            var firstRow = res.RawData.FirstOrDefault();
            if (firstRow != null)
            {
                var cpuStr = firstRow.GetValueOrDefault("cpu-load", "0");
                var freeMemStr = firstRow.GetValueOrDefault("free-memory", "0");
                var totalMemStr = firstRow.GetValueOrDefault("total-memory", "0");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CpuUsagePercent = double.TryParse(cpuStr, out var cpu) ? cpu : 0;
                    
                    if (double.TryParse(freeMemStr, out var free))
                        FreeMemoryMb = Math.Round(free / (1024.0 * 1024.0), 1);
                    
                    if (double.TryParse(totalMemStr, out var total))
                        TotalMemoryMb = Math.Round(total / (1024.0 * 1024.0), 1);
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ [NOC Polling Error] Failed to update system resources: {Message}", ex.Message);
        }
    }

    // ── Helpers ──
    private void AddAlert(AlertSeverity severity, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Avoid duplicate active alerts if message is identical and recently added
            var recent = Alerts.FirstOrDefault(a => string.Equals(a.Message, message, StringComparison.OrdinalIgnoreCase));
            if (recent != null && (DateTime.Now - recent.Timestamp).TotalMinutes < 2)
            {
                recent.Timestamp = DateTime.Now;
                return;
            }

            Alerts.Insert(0, new AlertItem
            {
                Timestamp = DateTime.Now,
                Severity = severity,
                Message = message
            });

            // Keep alerts log capped to 50 items
            if (Alerts.Count > 50)
            {
                Alerts.RemoveAt(Alerts.Count - 1);
            }
        });
    }

    private void ResetMetrics()
    {
        TotalClients = 0;
        ActiveVlansCount = 0;
        TotalDownloadMbps = 0;
        TotalUploadMbps = 0;
        HighestUsageVlanName = "—";
        CongestedVlansCount = 0;
        OfflineVlansCount = 0;
        CpuUsagePercent = 0;
        FreeMemoryMb = 0;
        TotalMemoryMb = 0;
    }

    public static bool IsIpInSubnet(string ipAddress, string subnet)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(subnet)) return false;
        try
        {
            var parts = subnet.Split('/');
            if (parts.Length != 2) return false;

            var subnetIp = IPAddress.Parse(parts[0]);
            var cidr = int.Parse(parts[1]);

            var ip = IPAddress.Parse(ipAddress);

            byte[] subnetBytes = subnetIp.GetAddressBytes();
            byte[] ipBytes = ip.GetAddressBytes();
            if (subnetBytes.Length != ipBytes.Length) return false;

            int maskBytes = cidr / 8;
            int maskBits = cidr % 8;

            for (int i = 0; i < maskBytes; i++)
            {
                if (subnetBytes[i] != ipBytes[i]) return false;
            }

            if (maskBits > 0)
            {
                byte mask = (byte)(0xFF << (8 - maskBits));
                if ((subnetBytes[maskBytes] & mask) != (ipBytes[maskBytes] & mask)) return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    public async Task ToggleVlanStatusAsync(VlanMonitorItem vlan)
    {
        if (vlan == null || !_activeRouterContext.IsConnected) return;

        try
        {
            var newDisabled = !vlan.IsDisabled;
            var parameters = new Dictionary<string, string>
            {
                { ".id", vlan.Name },
                { "disabled", newDisabled ? "yes" : "no" }
            };
            
            _logger.LogInformation("Attempting to toggle VLAN {Name} status to disabled={Disabled}...", vlan.Name, newDisabled);
            await _routerService.ExecuteCommandAsync("/interface/vlan/set", parameters);
            
            // Instantly update local UI status
            vlan.IsDisabled = newDisabled;
            if (newDisabled)
            {
                vlan.HealthStatus = VlanHealthStatus.Offline;
                vlan.DownloadSpeedMbps = 0;
                vlan.UploadSpeedMbps = 0;
                vlan.UtilizationPercent = 0;
            }
            
            AddAlert(AlertSeverity.Info, $"ℹ️ تم {(newDisabled ? "تعطيل" : "تفعيل")} الفيلان {vlan.DisplayName} بنجاح.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle VLAN status for {Name}", vlan.Name);
            MessageBox.Show($"فشل تعديل حالة الفيلان: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task EditVlanNameAsync(VlanMonitorItem vlan)
    {
        if (vlan == null) return;

        var dialog = new EditVlanNameDialog(vlan.LocalName ?? string.Empty);
        dialog.Owner = Application.Current.MainWindow;
        
        if (dialog.ShowDialog() == true)
        {
            var newName = dialog.VlanName;
            vlan.LocalName = newName;

            // Save to settings
            try
            {
                var dict = _settingsService.Get<Dictionary<string, string>>("LocalVlanNames") ?? new Dictionary<string, string>();
                if (string.IsNullOrEmpty(newName))
                {
                    dict.Remove(vlan.VlanId);
                }
                else
                {
                    dict[vlan.VlanId] = newName;
                }

                _settingsService.Set("LocalVlanNames", dict);
                await _settingsService.SaveAsync();
                
                _logger.LogInformation("VLAN Name mapping saved locally for ID {VlanId}: '{Name}'", vlan.VlanId, newName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save local VLAN name for ID {VlanId}", vlan.VlanId);
            }
        }
    }

    [RelayCommand]
    public async Task ConfigureMonitoringAsync(VlanMonitorItem vlan)
    {
        if (vlan == null) return;
        if (_activeRouterContext.CurrentRouter == null) return;

        var routerId = _activeRouterContext.CurrentRouter.Id;
        string currentIp = string.Empty;
        string currentDesc = string.Empty;
        bool enableMonitoring = true;

        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var config = await db.VlanMonitoringConfigs
                    .FirstOrDefaultAsync(c => c.RouterId == routerId && c.VlanId == vlan.VlanId);

                if (config != null)
                {
                    currentIp = config.DeviceIp;
                    currentDesc = config.Description ?? string.Empty;
                    enableMonitoring = config.EnableMonitoring;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load monitor config from database");
        }

        var dialog = new ConfigureMonitoringDialog(currentIp, currentDesc, enableMonitoring);
        dialog.Owner = Application.Current.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var config = await db.VlanMonitoringConfigs
                    .FirstOrDefaultAsync(c => c.RouterId == routerId && c.VlanId == vlan.VlanId);

                if (config == null)
                {
                    config = new MikroTikVoucherPrinter.Domain.Entities.Platform.VlanMonitoringConfig
                    {
                        RouterId = routerId,
                        VlanId = vlan.VlanId
                    };
                    db.VlanMonitoringConfigs.Add(config);
                }

                config.DeviceIp = dialog.DeviceIp;
                config.Description = dialog.Description;
                config.EnableMonitoring = dialog.EnableMonitoring;

                await db.SaveChangesAsync();

                // Update UI properties
                vlan.DeviceIp = dialog.DeviceIp;
                vlan.DeviceStatus = dialog.EnableMonitoring ? "Offline" : "NotMonitored";
                if (!dialog.EnableMonitoring)
                {
                    vlan.LatencyMs = 0;
                    vlan.LastSeen = null;
                }

                _logger.LogInformation("NOC monitor config saved for VLAN {VlanId}: IP {IP}, Enabled: {Enabled}", vlan.VlanId, dialog.DeviceIp, dialog.EnableMonitoring);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save monitor config to database");
                MessageBox.Show($"فشل حفظ إعدادات المراقبة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        StopPolling();
        _eventBus.UnsubscribeAll(this);
        GC.SuppressFinalize(this);
    }
}
