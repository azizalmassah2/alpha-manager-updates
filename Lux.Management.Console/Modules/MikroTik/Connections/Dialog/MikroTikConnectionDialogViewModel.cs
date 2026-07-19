using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Modules.MikroTik.Connections.Services;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.MikroTik.Providers;
using Lux.Platform.Abstractions.Interfaces;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Dialog;

public partial class MikroTikConnectionDialogViewModel : ObservableObject
{
    private readonly IMikroTikDiscoveryService _discoveryService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterRepository _routerRepository;
    private readonly ISecureStorageService _secureStorageService;
    private readonly IRouterOsProvider _routerOsProvider;

    public MikroTikConnectionDialogViewModel(
        IMikroTikDiscoveryService discoveryService,
        IActiveRouterContext activeRouterContext,
        IRouterRepository routerRepository,
        ISecureStorageService secureStorageService,
        IRouterOsProvider routerOsProvider)
    {
        _discoveryService = discoveryService;
        _activeRouterContext = activeRouterContext;
        _routerRepository = routerRepository;
        _secureStorageService = secureStorageService;
        _routerOsProvider = routerOsProvider;

        // Load saved routers AND start discovery concurrently on open
        _ = LoadSavedRoutersAsync();
        _ = RefreshDiscoveryAsync();
    }

    // ── Form Fields ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _host = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _port = "8728";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _password = string.Empty;

    // ── State ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ── Saved Routers (from DB) ───────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSavedRouters))]
    private ObservableCollection<SavedRouterRow> _savedRouters = new();

    public bool HasSavedRouters => SavedRouters.Count > 0;

    // ── Discovered Devices (MNDP) ─────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiscoveredDevices))]
    private ObservableCollection<DiscoveredDevice> _discoveredDevices = new();

    public bool HasDiscoveredDevices => DiscoveredDevices.Count > 0;

    private DiscoveredDevice? _selectedDevice;
    public DiscoveredDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value) && value != null)
            {
                // Auto-fill from discovered device
                Host = value.IpAddress;
                Port = "8728";
                Username = "admin";
                Password = string.Empty;
            }
        }
    }

    // ── Current active router ─────────────────────────────────────────────────

    public string? ActiveRouterHost => _activeRouterContext.CurrentRouter?.Host;
    public bool IsConnectedToAny => _activeRouterContext.CurrentRouter != null;

    public Action? CloseAction { get; set; }

    // ── CanExecute ────────────────────────────────────────────────────────────

    private bool CanConnect()
    {
        if (string.IsNullOrWhiteSpace(Host)) return false;
        if (string.IsNullOrWhiteSpace(Username)) return false;
        // Password can be empty for some routers (initial setup)
        if (!int.TryParse(Port, out int p) || p < 1 || p > 65535) return false;
        return true;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Load saved routers from the database to show in the "Saved Routers" section.</summary>
    [RelayCommand]
    private async Task LoadSavedRoutersAsync()
    {
        try
        {
            var list = await _routerRepository.GetAllAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SavedRouters.Clear();
                foreach (var r in list)
                {
                    SavedRouters.Add(new SavedRouterRow
                    {
                        Router = r,
                        IsActive = _activeRouterContext.CurrentRouter?.Id == r.Id
                    });
                }
            });
        }
        catch { /* non-fatal */ }
    }

    /// <summary>Run MNDP discovery and populate the discovered devices list.</summary>
    [RelayCommand]
    private async Task RefreshDiscoveryAsync()
    {
        IsDiscovering = true;
        ErrorMessage = string.Empty;
        try
        {
        var devices = await _discoveryService.DiscoverDevicesAsync();
        
        System.Console.WriteLine($"[DIAGNOSTIC] Selected Thread ID before Dispatcher: {Environment.CurrentManagedThreadId}");

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Console.WriteLine($"[DIAGNOSTIC] Selected Thread ID inside Dispatcher (UI Thread): {Environment.CurrentManagedThreadId}");
            System.Console.WriteLine($"[DIAGNOSTIC] DiscoveredDevices Count Before Add = {DiscoveredDevices.Count}");
            
            DiscoveredDevices.Clear();
            foreach (var device in devices)
            {
                DiscoveredDevices.Add(device);
                System.Console.WriteLine($"[DIAGNOSTIC] Device Added: {device.IpAddress}");
            }
            
            System.Console.WriteLine($"[DIAGNOSTIC] DiscoveredDevices Count After Add = {DiscoveredDevices.Count}");
            
            OnPropertyChanged(nameof(HasDiscoveredDevices));
            System.Console.WriteLine($"[DIAGNOSTIC] PropertyChanged Fired for HasDiscoveredDevices");
        });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"تعذّر الاكتشاف التلقائي: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    /// <summary>Fill the form from a saved router row and optionally switch to it directly.</summary>
    [RelayCommand]
    private async Task ConnectSavedRouterAsync(SavedRouterRow row)
    {
        if (row?.Router == null) return;
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            await _activeRouterContext.SwitchRouterAsync(row.Router);
            await LoadSavedRoutersAsync(); // refresh active state
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = GetFriendlyErrorMessage(ex);
        }
        finally { IsBusy = false; }
    }

    /// <summary>Disconnect from the currently active router.</summary>
    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            await _activeRouterContext.DisconnectAsync();
            await LoadSavedRoutersAsync();
        }
        catch { /* ignore */ }
    }

    /// <summary>Fill the form from a saved router for manual editing, without connecting.</summary>
    [RelayCommand]
    private void EditSavedRouter(SavedRouterRow row)
    {
        if (row?.Router == null) return;
        Host = row.Router.Host;
        Port = row.Router.Port.ToString();
        Username = row.Router.Username;
        Password = string.Empty; // never prefill password for security
    }

    [RelayCommand]
    private async Task DeleteSavedRouterAsync(SavedRouterRow row)
    {
        if (row?.Router == null) return;
        try
        {
            await _routerRepository.DeleteAsync(row.Router.Id);
            await LoadSavedRoutersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"تعذّر الحذف: {ex.Message}";
        }
    }

    /// <summary>Fill the form from a clicked discovered device card.</summary>
    [RelayCommand]
    private void FillFromDiscovered(DiscoveredDevice device)
    {
        if (device == null) return;
        Host = device.IpAddress;
        Port = "8728";
        Username = "admin";
        Password = string.Empty;
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseAction?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Host)) { ErrorMessage = "الحقل مطلوب: عنوان IP."; return; }
        if (string.IsNullOrWhiteSpace(Username)) { ErrorMessage = "الحقل مطلوب: اسم المستخدم."; return; }
        if (!int.TryParse(Port, out int portNum) || portNum < 1 || portNum > 65535)
        { ErrorMessage = "المنفذ يجب أن يكون رقماً بين 1 و 65535."; return; }

        IsBusy = true;
        StatusMessage = "جاري الاتصال…";

        try
        {
            var options = new MikroTikConnectionOptions
            {
                Host = Host,
                Port = portNum,
                Username = Username,
                Password = Password,
                UseSsl = false,
                ProviderType = RouterOsProviderType.Api
            };

            var connectResult = await _routerOsProvider.ConnectAsync(options);
            if (!connectResult.IsSuccess)
            {
                ErrorMessage = "تعذّر الاتصال بالخادم. تحقق من العنوان والمنفذ.";
                return;
            }

            // ── Fetch Identity name from /system/identity/print ──
            string identityName = Host; // fallback = IP
            StatusMessage = "جاري جلب هوية الجهاز…";

            try
            {
                var idResult = await _routerOsProvider.ExecuteAsync(
                    new MikroTikCommand { Command = "/system/identity/print" });

                if (idResult.IsSuccess && idResult.Value?.RawData?.Count > 0)
                {
                    var dict = idResult.Value.RawData[0];
                    if (dict.TryGetValue("name", out var nameVal) && !string.IsNullOrWhiteSpace(nameVal))
                    {
                        identityName = nameVal.Trim();
                    }
                }
            }
            catch { /* identity fetch non-fatal, use IP fallback */ }
            
            // Also try MNDP discovered identity as fallback
            if (identityName == Host)
            {
                var mndpMatch = DiscoveredDevices
                    .FirstOrDefault(d => d.IpAddress == Host);
                if (mndpMatch != null && !string.IsNullOrEmpty(mndpMatch.Identity))
                    identityName = mndpMatch.Identity;
            }

            // ── Save or update Router in DB ──
            StatusMessage = "جاري الحفظ…";
            var existingList = await _routerRepository.GetAllAsync();
            var existing = existingList.FirstOrDefault(r =>
                r.Host.Equals(Host, StringComparison.OrdinalIgnoreCase) && r.Port == portNum);

            Router router;
            if (existing != null)
            {
                // Update existing entry
                existing.DisplayName = identityName;
                existing.Username = Username;
                existing.EncryptedPassword = _secureStorageService.Encrypt(Password);
                await _routerRepository.UpdateAsync(existing);
                router = existing;
            }
            else
            {
                // Create new entry
                router = new Router
                {
                    DisplayName = identityName,
                    Host = Host,
                    Port = portNum,
                    Username = Username,
                    EncryptedPassword = _secureStorageService.Encrypt(Password)
                };
                await _routerRepository.AddAsync(router);
            }

            // ── Connect and switch ──
            await _activeRouterContext.SwitchRouterAsync(router);
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = GetFriendlyErrorMessage(ex);
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
        }
    }

    private string GetFriendlyErrorMessage(Exception ex)
    {
        var message = ex.ToString().ToLowerInvariant();

        // 1. Authentication / Login failures
        if (message.Contains("login") || 
            message.Contains("authenticate") || 
            message.Contains("credentials") || 
            message.Contains("password") || 
            message.Contains("user"))
        {
            return "❌ اسم المستخدم أو كلمة المرور غير صحيحة. يرجى التحقق من بيانات الدخول.";
        }

        // 2. Host not found / Invalid IP / Unreachable
        if (message.Contains("no such host") || 
            message.Contains("host is unreachable") || 
            message.Contains("host unreachable") || 
            message.Contains("timed out") || 
            message.Contains("timeout"))
        {
            return "❌ تعذر العثور على الجهاز أو أن عنوان IP غير صحيح أو غير متصل بالشبكة.";
        }

        // 3. Connection refused (API disabled or wrong port)
        if (message.Contains("refused") || 
            message.Contains("actively refused") || 
            message.Contains("connection reset"))
        {
            return "❌ تم رفض الاتصال. يرجى التأكد من تشغيل خدمة الـ API على منفذ 8728 في المايكروتك (من قائمة IP -> Services).";
        }

        // Fallback generic message with details
        return $"❌ تعذر الاتصال بالخادم: {ex.Message}";
    }

}

/// <summary>Wraps a Router entity with a computed IsActive flag for the saved routers list.</summary>
public class SavedRouterRow : ObservableObject
{
    public Router Router { get; set; } = null!;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
