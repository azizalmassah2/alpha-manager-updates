using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

/// <summary>
/// ViewModel ط§ظ„طµظپط­ط© ط§ظ„ط±ط¦ظٹط³ظٹط© (Dashboard)
/// </summary>
public partial class DashboardViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly Application.Interfaces.IVoucherQueryService _voucherQuery;

    public DashboardViewModel(
        ISettingsService settingsService,
        Application.Interfaces.IVoucherQueryService voucherQuery,
        ILogger<DashboardViewModel> logger)
        : base(logger)
    {
        _settingsService = settingsService;
        _voucherQuery = voucherQuery;
        Title = "ط§ظ„ط±ط¦ظٹط³ظٹط©";
    }

    private DashboardStatsDto _stats = new();
    public DashboardStatsDto Stats
    {
        get => _stats;
        set => SetProperty(ref _stats, value);
    }

    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
    public ConnectionStatus MikroTikStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public string ConnectionStatusText => MikroTikStatus switch
    {
        ConnectionStatus.Connected => "ظ…طھطµظ„",
        ConnectionStatus.Connecting => "ط¬ط§ط±ظٹ ط§ظ„ط§طھطµط§ظ„...",
        ConnectionStatus.Disconnected => "ط؛ظٹط± ظ…طھطµظ„",
        ConnectionStatus.Failed => "ظپط´ظ„ ط§ظ„ط§طھطµط§ظ„",
        _ => "ط؛ظٹط± ظ…ط¹ط±ظˆظپ"
    };

    [ObservableProperty] private int _totalMikroTikUsers = 0;
    [ObservableProperty] private int _activeHotspotUsers = 0;
    [ObservableProperty] private string _cpuLoad = "0%";
    [ObservableProperty] private string _uptime = "00:00:00";

    public override async Task InitializeAsync(object? parameter = null)
    {
        await ExecuteBusyAsync(async (token) =>
        {
            // 1. ط¬ظ„ط¨ ط§ظ„ط¥ط­طµط§ط¦ظٹط§طھ ظ…ظ† ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ ط§ظ„ظ…ط­ظ„ظٹط© (ط§ظ„ظƒط±ظˆطھ ط§ظ„ظ…ظڈط¯ط§ط±ط© ط¹ط¨ط± ظ„ظˆظƒط³ ظƒط§ط±ط¯)
            try
            {
                var vouchers = await _voucherQuery.GetAllVouchersAsync(token);
                Stats = new DashboardStatsDto
                {
                    TotalVouchers   = vouchers.Count,
                    SyncedVouchers  = vouchers.Count(v => v.SyncStatus == SyncStatus.Synced),
                    PendingVouchers = vouchers.Count(v => v.SyncStatus == SyncStatus.Pending),
                    FailedVouchers  = vouchers.Count(v => v.SyncStatus == SyncStatus.Failed),
                    UsedVouchers    = vouchers.Count(v => v.Status == VoucherStatus.Used),
                    ExpiredVouchers = vouchers.Count(v => v.Status == VoucherStatus.Expired)
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("ظپط´ظ„ ظپظٹ ط¬ظ„ط¨ ط¥ط­طµط§ط¦ظٹط§طھ ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ: {Err}", ex.Message);
            }
            
            // 2. ط³ط­ط¨ ط§ظ„ط¨ظٹط§ظ†ط§طھ ط§ظ„ط­ظٹط© ظ…ظ† ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ
            await Task.Run(() => 
            {
                try
                {
                    var host = _settingsService.Get("MikroTik.Host", "");
                    var user = _settingsService.Get("MikroTik.Username", "");
                    var pass = _settingsService.Get("MikroTik.Password", "");

                    if (!string.IsNullOrEmpty(host))
                    {
                        using var connection = tik4net.ConnectionFactory.CreateConnection(tik4net.TikConnectionType.Api);
                        connection.SendTimeout = 10000;
                        connection.ReceiveTimeout = 10000;
                        connection.Open(host, user, pass);

                        // ط§ط³طھط®ط±ط§ط¬ ط¥ط¬ظ…ط§ظ„ظٹ ط§ظ„ظƒط±ظˆطھ ظ…ظ† ط§ظ„ظ€ (User Manager) ط¨ط´ظƒظ„ ط£ط³ط§ط³ظٹ
                        try 
                        {
                            TotalMikroTikUsers = connection.CreateCommandAndParameters("/tool/user-manager/user/print").ExecuteList().Count();
                        }
                        catch 
                        {
                            // ظƒط®ظٹط§ط± ط¨ط¯ظٹظ„ ط¥ط°ط§ ظ„ظ… ظٹظƒظ† ط§ظ„ظٹظˆط²ط± ظ…ط§ظ†ط¬ط± ظ…ظڈظ†طµط¨ط§ظ‹طŒ ظ†ط¬ظ„ط¨ ط§ظ„ط¹ط¯ط¯ ظ…ظ† ط§ظ„ظ‡ظˆطھ ط³ط¨ظˆطھ
                            try { TotalMikroTikUsers = connection.CreateCommandAndParameters("/ip/hotspot/user/print").ExecuteList().Count(); } catch { TotalMikroTikUsers = 0; }
                        }

                        // ط§ظ„ظ…طھطµظ„ظٹظ† ط­ط§ظ„ظٹط§ظ‹
                        try
                        {
                            ActiveHotspotUsers = connection.CreateCommandAndParameters("/ip/hotspot/active/print").ExecuteList().Count();
                        }
                        catch { ActiveHotspotUsers = 0; }

                        // ط³ط­ط¨ ط§ط³طھظ‡ظ„ط§ظƒ ط§ظ„ظ…ط¹ط§ظ„ط¬ ظˆظ…ط¯ط© ط§ظ„طھط´ط؛ظٹظ„
                        try
                        {
                            var resource = connection.CreateCommandAndParameters("/system/resource/print").ExecuteList().FirstOrDefault();
                            if (resource != null)
                            {
                                var cpuWord = resource.Words.FirstOrDefault(w => w.Key == "cpu-load");
                                var uptimeWord = resource.Words.FirstOrDefault(w => w.Key == "uptime");
                                
                                if (!string.IsNullOrEmpty(cpuWord.Value)) CpuLoad = cpuWord.Value + "%";
                                if (!string.IsNullOrEmpty(uptimeWord.Value)) Uptime = uptimeWord.Value;
                            }
                        }
                        catch { }

                        MikroTikStatus = ConnectionStatus.Connected;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("طھط¹ط°ط± ط¬ظ„ط¨ ط§ظ„ط¥ط­طµط§ط¦ظٹط§طھ ط§ظ„ط­ظٹط© ظ„ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ: {Error}", ex.Message);
                    MikroTikStatus = ConnectionStatus.Disconnected;
                    // طھظˆط¬ظٹظ‡ ط§ظ„ط®ط·ط£ ظ„ظ„ط¸ظ‡ظˆط± ظپظٹ ظˆط§ط¬ظ‡ط© ط§ظ„ظ…ط³طھط®ط¯ظ…
                    throw new Exception($"طھط¹ط°ط± ط³ط­ط¨ ط§ظ„ط¨ظٹط§ظ†ط§طھ ط§ظ„ظ…طھطµظ„ط© ط¨ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ: {ex.Message}");
                }
            });

            Logger.LogInformation("âœ… [Dashboard] طھظ… طھط­ط¯ظٹط« ط§ظ„ظˆط§ط¬ظ‡ط© ط§ظ„ط­ظٹط©.");
        }, "ط¬ط§ط±ظٹ ط¬ظ„ط¨ ظ†ط¨ط¶ ط§ظ„ظ†ط¸ط§ظ… ظˆط§ط¬ظ‡ط§طھ ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ...");
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await InitializeAsync();
    }
    
    // Timer ظ„ظ„طھط­ط¯ظٹط« ط§ظ„طھظ„ظ‚ط§ط¦ظٹ ظ„ظ„ط¥ط­طµط§ط¦ظٹط§طھ ط§ظ„ط­ظٹط© ظپظ‚ط· ط¨ط¯ظˆظ† طھط¬ظ…ظٹط¯ ط§ظ„ظˆط§ط¬ظ‡ط© (ط§ط®طھظٹط§ط±ظٹطŒ ظٹظ…ظƒظ† طھظپط¹ظٹظ„ظ‡ ظ„ط§ط­ظ‚ط§ظ‹ ظ„ظˆ ط§ط­طھط¬ظ†ط§ظ‡)
    // ط­ط§ظ„ظٹط§ظ‹ ظ†ط¹طھظ…ط¯ ط¹ظ„ظ‰ ط²ط± ط§ظ„طھط­ط¯ظٹط« ط£ظˆ ط¹ظ†ط¯ ظپطھط­ ط§ظ„طµظپط­ط©.
}
