using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace Lux.Management.Console.Modules.MikroTik.Connections.ViewModels;

public partial class RouterDetailsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 8728;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public Action? CloseAction { get; set; }
    
    public bool DialogResult { get; private set; }
    public Router? ResultRouter { get; private set; }

    private readonly Router? _existingRouter;

    public RouterDetailsViewModel(Router? existingRouter = null)
    {
        _existingRouter = existingRouter;
        if (_existingRouter != null)
        {
            DisplayName = _existingRouter.DisplayName;
            Host = _existingRouter.Host;
            Port = _existingRouter.Port;
            Username = _existingRouter.Username;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Username))
        {
            return;
        }

        ResultRouter = _existingRouter ?? new Router();
        ResultRouter.DisplayName = DisplayName;
        ResultRouter.Host = Host;
        ResultRouter.Port = Port;
        ResultRouter.Username = Username;
        
        if (!string.IsNullOrWhiteSpace(Password))
        {
            ResultRouter.EncryptedPassword = Password; 
        }

        DialogResult = true;
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        CloseAction?.Invoke();
    }
}
