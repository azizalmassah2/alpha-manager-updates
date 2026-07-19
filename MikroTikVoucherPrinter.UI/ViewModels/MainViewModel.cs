using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace MikroTikVoucherPrinter.UI.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private BaseViewModel? _currentViewModel;

    [ObservableProperty]
    private string _currentPageKey = string.Empty;

    [ObservableProperty]
    private bool _isMenuExpanded = true;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private string _themeIcon = "\uE708"; // Moon icon

    public MainViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        ISettingsService settingsService,
        ILogger<MainViewModel> logger)
        : base(logger)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _settingsService = settingsService;
        Title = "لوكس كارد";

        _navigationService.PageChanged += OnPageChanged;
        _themeService.ThemeChanged += OnThemeChanged;

        IsDarkTheme = _themeService.CurrentTheme == AppTheme.Dark;
        ThemeIcon = IsDarkTheme ? "\uE706" : "\uE708";

        _pingTimer = new System.Timers.Timer(15000);
        _pingTimer.Elapsed += PingTimer_Elapsed;
        _pingTimer.Start();
        
        Task.Run(() => CheckConnection());
    }

    [ObservableProperty] private bool _isMikroTikConnected;
    [ObservableProperty] private string _mikroTikConnectionText = "غير متصل بالمايكروتيك";
    private readonly System.Timers.Timer _pingTimer;
    private bool _connectionMonitorDisposed;

    public void StopConnectionMonitoring()
    {
        if (_connectionMonitorDisposed) return;
        _connectionMonitorDisposed = true;
        try
        {
            _pingTimer.Stop();
            _pingTimer.Elapsed -= PingTimer_Elapsed;
            _pingTimer.Dispose();
        }
        catch (ObjectDisposedException) { }
    }

    private async void PingTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        await Task.Run(() => CheckConnection());
    }

    private void CheckConnection()
    {
        try
        {
            var host = _settingsService.Get("MikroTik.Host", "");
            var user = _settingsService.Get("MikroTik.Username", "");
            var pass = _settingsService.Get("MikroTik.Password", "");

            if (string.IsNullOrEmpty(host)) 
            {
                SetDisconnectedState("الرجاء ضبط الإعدادات");
                return;
            }

            using var connection = tik4net.ConnectionFactory.CreateConnection(tik4net.TikConnectionType.Api);
            connection.SendTimeout = 2000;
            connection.ReceiveTimeout = 2000;
            connection.Open(host, user, pass);

            var identitySentence = connection.CreateCommandAndParameters("/system/identity/print").ExecuteList().FirstOrDefault();
            string identity = "MikroTik";
            if (identitySentence != null)
            {
                var nameWord = identitySentence.Words.FirstOrDefault(w => w.Key == "name");
                if (!string.IsNullOrEmpty(nameWord.Value)) identity = nameWord.Value;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsMikroTikConnected = true;
                MikroTikConnectionText = $"متصل 🟢 ({identity})";
            });
        }
        catch
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => SetDisconnectedState("غير متصل 🔴"));
        }
    }

    private void SetDisconnectedState(string message)
    {
        IsMikroTikConnected = false;
        MikroTikConnectionText = message;
    }

    private void OnThemeChanged(AppTheme theme)
    {
        IsDarkTheme = theme == AppTheme.Dark;
        ThemeIcon = IsDarkTheme ? "\uE706" : "\uE708"; 
        Logger.LogInformation("تم تبديل الثيم إلى: {Theme}", theme);
    }

    private void OnPageChanged(string pageKey)
    {
        CurrentPageKey = pageKey;
        Logger.LogInformation("تم التنقل إلى: {PageKey}", pageKey);
    }

    [RelayCommand]
    private void NavigateTo(string pageKey)
    {
        if (CurrentViewModel is MikroTikVoucherPrinter.UI.ViewModels.Pages.GenerateVoucherViewModel genVm && genVm.IsGenerating)
        {
            return;
        }
        
        _navigationService.NavigateTo(pageKey);
    }

    [RelayCommand]
    private void ToggleMenu()
    {
        IsMenuExpanded = !IsMenuExpanded;
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentViewModel is MikroTikVoucherPrinter.UI.ViewModels.Pages.GenerateVoucherViewModel genVm && genVm.IsGenerating)
        {
            return;
        }

        if (_navigationService.CanGoBack)
            _navigationService.GoBack();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
    }
}
