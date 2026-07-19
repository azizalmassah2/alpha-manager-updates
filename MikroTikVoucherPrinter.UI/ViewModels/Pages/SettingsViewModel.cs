using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

/// <summary>
/// ViewModel طµظپط­ط© ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ ط§ظ„ط¹ط§ظ…ط© (ط§ظ„ظ…ط¸ظ‡ط± + ط¹ط±ط¶ ظ…ط¹ظ„ظˆظ…ط§طھ ط§ظ„ط§طھطµط§ظ„)
/// ط¨ظٹط§ظ†ط§طھ ط§ظ„ط§طھطµط§ظ„ طھظڈط¯ط§ط± ط§ظ„ط¢ظ† ظ…ظ† LoginWindow
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService    _themeService;

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        ILogger<SettingsViewModel> logger)
        : base(logger)
    {
        _settingsService = settingsService;
        _themeService    = themeService;
        Title = "ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ";
    }

    // â•گâ•گâ•گ ط¨ظٹط§ظ†ط§طھ ط§ظ„ط§طھطµط§ظ„ ط§ظ„ط­ط§ظ„ظٹط© (ظ„ظ„ط¹ط±ط¶ ظپظ‚ط·) â•گâ•گâ•گ
    [ObservableProperty] private string _currentHost     = "";
    [ObservableProperty] private string _currentUsername = "";
    [ObservableProperty] private bool   _isConnected     = false;

    public override async Task InitializeAsync(object? parameter = null)
    {
        await ExecuteBusyAsync(async (token) =>
        {
            CurrentHost     = _settingsService.Get("MikroTik.Host",    "ط؛ظٹط± ظ…ط­ط¯ط¯");
            CurrentUsername = _settingsService.Get("MikroTik.Username", "ط؛ظٹط± ظ…ط­ط¯ط¯");

            // ط§ظ„طھط­ظ‚ظ‚ ط§ظ„ط³ط±ظٹط¹ ظ…ظ† ط­ط§ظ„ط© ط§ظ„ط§طھطµط§ظ„
            await Task.Run(() =>
            {
                try
                {
                    var host = _settingsService.Get("MikroTik.Host",    "");
                    var user = _settingsService.Get("MikroTik.Username", "");
                    var pass = _settingsService.Get("MikroTik.Password", "");

                    if (!string.IsNullOrEmpty(host))
                    {
                        using var conn = tik4net.ConnectionFactory.CreateConnection(tik4net.TikConnectionType.Api);
                        conn.SendTimeout    = 3000;
                        conn.ReceiveTimeout = 3000;
                        conn.Open(host, user, pass);
                        System.Windows.Application.Current.Dispatcher.Invoke(() => IsConnected = true);
                    }
                }
                catch
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => IsConnected = false);
                }
            }, token);

            Logger.LogInformation("طھظ… طھط­ظ…ظٹظ„ طµظپط­ط© ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ");
        }, "ط¬ط§ط±ظٹ ط§ظ„طھط­ظ‚ظ‚ ظ…ظ† ط§ظ„ط§طھطµط§ظ„...");
    }

    // â•گâ•گâ•گ طھط¨ط¯ظٹظ„ ط§ظ„ط«ظٹظ… â•گâ•گâ•گ
    [RelayCommand]
    private void SwitchToDark()
    {
        _themeService.SetTheme(AppTheme.Dark);
        StatusMessage = "âœ“ طھظ… ط§ظ„طھط¨ط¯ظٹظ„ ط¥ظ„ظ‰ ط§ظ„ظˆط¶ط¹ ط§ظ„ط¯ط§ظƒظ†";
        Logger.LogInformation("طھظ… ط§ظ„طھط¨ط¯ظٹظ„ ط¥ظ„ظ‰ ط§ظ„ط«ظٹظ… ط§ظ„ط¯ط§ظƒظ†");
    }

    [RelayCommand]
    private void SwitchToLight()
    {
        _themeService.SetTheme(AppTheme.Light);
        StatusMessage = "âœ“ طھظ… ط§ظ„طھط¨ط¯ظٹظ„ ط¥ظ„ظ‰ ط§ظ„ظˆط¶ط¹ ط§ظ„ظپط§طھط­";
        Logger.LogInformation("طھظ… ط§ظ„طھط¨ط¯ظٹظ„ ط¥ظ„ظ‰ ط§ظ„ط«ظٹظ… ط§ظ„ظپط§طھط­");
    }

    // â•گâ•گâ•گ طھط؛ظٹظٹط± ط§ظ„ط§طھطµط§ظ„ â€” ظٹظڈط·ظ„ظ‚ ط­ط¯ط«ط§ظ‹ ظ„ظپطھط­ LoginWindow ظ…ظ† App.xaml.cs â•گâ•گâ•گ
    public event Action? ChangeConnectionRequested;

    [RelayCommand]
    private void ChangeConnection()
    {
        ChangeConnectionRequested?.Invoke();
    }
}
