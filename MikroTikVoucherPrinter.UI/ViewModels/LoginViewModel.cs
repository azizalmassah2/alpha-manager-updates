using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Services;
using tik4net;

namespace MikroTikVoucherPrinter.UI.ViewModels;

public class SavedDeviceModel
{
    public string IpAddress { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public partial class LoginViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<LoginViewModel> _logger;
    private readonly IDialogService _dialogService;

    public LoginViewModel(ISettingsService settingsService, ILogger<LoginViewModel> logger, IDialogService dialogService)
    {
        _settingsService = settingsService;
        _logger = logger;
        _dialogService = dialogService;

        // تحميل القيم المحفوظة
        Host     = _settingsService.Get("MikroTik.Host", "192.168.88.1");
        Port     = _settingsService.Get("MikroTik.Port", 8728);
        Username = _settingsService.Get("MikroTik.Username", "admin");
        Password = _settingsService.Get("MikroTik.Password", "");

        // تحميل الأجهزة المحفوظة
        var savedList = _settingsService.Get("MikroTik.SavedDevices", new List<SavedDeviceModel>());
        foreach (var item in savedList)
        {
            SavedDevices.Add(item);
        }
    }

    // ═══ خصائص الاتصال ═══
    [ObservableProperty] private string _host = "192.168.88.1";
    [ObservableProperty] private int    _port = 8728;
    [ObservableProperty] private string _username = "admin";
    [ObservableProperty] private string _password = "";

    // ═══ حالة الواجهة ═══
    [ObservableProperty] private bool   _isConnecting = false;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool   _hasError = false;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool   _isDiscovering = false;

    // أجهزة المايكروتك المكتشفة على الشبكة
    public ObservableCollection<MikroTikDeviceModel> DiscoveredDevices { get; } = new();

    private MikroTikDeviceModel? _selectedDevice;
    public MikroTikDeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value) && value != null)
                Host = value.IpAddress; // تعبئة IP تلقائياً
        }
    }

    public ObservableCollection<SavedDeviceModel> SavedDevices { get; } = new();

    private SavedDeviceModel? _selectedSavedDevice;
    public SavedDeviceModel? SelectedSavedDevice
    {
        get => _selectedSavedDevice;
        set
        {
            if (SetProperty(ref _selectedSavedDevice, value) && value != null)
            {
                Host = value.IpAddress;
                Username = value.Username;
                Password = value.Password;
            }
        }
    }

    // ═══ نتيجة تسجيل الدخول: true = نجاح ═══
    public bool LoginSucceeded { get; private set; } = false;
    public event Action? OnLoginSucceeded;

    // ═══ الأوامر ═══
    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsConnecting = true;
        HasError = false;
        ErrorMessage = "";
        StatusMessage = "جاري الاتصال بالمايكروتك...";

        try
        {
            string? stableDeviceId = null;
            await Task.Run(() =>
            {
                using var conn = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                conn.SendTimeout    = 5000;
                conn.ReceiveTimeout = 5000;
                conn.Open(Host, Username, Password);

                stableDeviceId = MikroTikRouterSerialReader.TryReadStableDeviceId(conn);

                // التحقق من أن الاتصال حي فعلاً
                var identity = conn.CreateCommandAndParameters("/system/identity/print")
                                   .ExecuteList()
                                   .FirstOrDefault();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var name = identity?.Words.FirstOrDefault(w => w.Key == "name").Value ?? "MikroTik";
                    StatusMessage = $"✅ متصل بـ: {name}";
                });
            });

            // حفظ بيانات الاتصال إذا طلب المستخدم
            _settingsService.Set("MikroTik.Host",     Host);
            _settingsService.Set("MikroTik.Port",     Port);
            _settingsService.Set("MikroTik.Username", Username);
            _settingsService.Set("MikroTik.Password", Password);
            if (!string.IsNullOrWhiteSpace(stableDeviceId))
                _settingsService.Set("RouterSerial", stableDeviceId);
            await _settingsService.SaveAsync();

            LoginSucceeded = true;
            _logger.LogInformation("✅ تسجيل الدخول نجح للمضيف {Host}", Host);

            LoginSucceeded = true;
            OnLoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            var lowerMsg = ex.Message.ToLower();
            string userFriendlyMessage;

            if (lowerMsg.Contains("invalid user name or password") || lowerMsg.Contains("wrong username") || lowerMsg.Contains("invalid password"))
            {
                userFriendlyMessage = "اسم المستخدم أو كلمة المرور غير صحيحة.";
            }
            else if (lowerMsg.Contains("failed to respond") || lowerMsg.Contains("timeout"))
            {
                userFriendlyMessage = "المايكروتك لا يستجيب. يرجى التأكد من تفعيل خدمة الـ API (المنفذ 8728) في إعدادات المايكروتك (IP -> Services).";
            }
            else if (lowerMsg.Contains("actively refused it"))
            {
                userFriendlyMessage = "المايكروتك يرفض الاتصال. يرجى التأكد من تشغيل الـ API.";
            }
            else
            {
                userFriendlyMessage = $"فشل الاتصال: {ex.Message}";
            }

            HasError = true;
            ErrorMessage = userFriendlyMessage;
            StatusMessage = "";
            _logger.LogWarning("❌ فشل الاتصال بـ {Host}: {Error}", Host, ex.Message);

            // عرض رسالة منبثقة للمستخدم
            await _dialogService.ShowErrorAsync("خطأ في الاتصال", userFriendlyMessage);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private bool CanLogin() => !IsConnecting && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username);

    [RelayCommand]
    private async Task SaveDeviceAsync()
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Username))
        {
            StatusMessage = "يرجى تعبئة الحقول (الآيبي واسم المستخدم) قبل الحفظ.";
            return;
        }

        var existing = SavedDevices.FirstOrDefault(x => x.IpAddress == Host);
        if (existing != null)
        {
            existing.Username = Username;
            existing.Password = Password;
        }
        else
        {
            SavedDevices.Add(new SavedDeviceModel
            {
                IpAddress = Host,
                Username = Username,
                Password = Password
            });
        }

        _settingsService.Set("MikroTik.SavedDevices", SavedDevices.ToList());
        await _settingsService.SaveAsync();
        StatusMessage = "✅ تم حفظ بيانات الجهاز بنجاح.";
    }

    partial void OnHostChanged(string value)     => LoginCommand.NotifyCanExecuteChanged();
    partial void OnUsernameChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnIsConnectingChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task DiscoverNetworkAsync()
    {
        IsDiscovering = true;
        StatusMessage = "جاري فحص الشبكة المحلية...";
        DiscoveredDevices.Clear();

        try
        {
            var scanner = new MikroTikDiscoveryService();
            var devices = await scanner.DiscoverAsync(3000);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var d in devices) DiscoveredDevices.Add(d);
                StatusMessage = devices.Count > 0
                    ? $"تم اكتشاف {devices.Count} جهاز. اختر جهازاً لتعبئة IP تلقائياً."
                    : "لم يُعثر على أي جهاز مايكروتك على الشبكة المحلية.";
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الفحص: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }
}
