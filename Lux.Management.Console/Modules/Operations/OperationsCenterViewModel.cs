using CommunityToolkit.Mvvm.ComponentModel;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;

using Lux.Management.Console.ViewModels;
namespace Lux.Management.Console.Modules.Operations.ViewModels;

public partial class OperationsCenterViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ViewModelBase? _currentCenterViewModel;

    public OperationsCenterViewModel(
        IPermissionService permissionService, 
        IEventBus eventBus,
        INavigationService navigationService,
        RouterOperationsViewModel routerVm,
        ModemOperationsViewModel modemVm,
        WirelessOperationsViewModel wirelessVm,
        OperationHistoryViewModel historyVm) 
        : base(permissionService, eventBus)
    {
        _navigationService = navigationService;
        RouterOperationsVm = routerVm;
        ModemOperationsVm = modemVm;
        WirelessOperationsVm = wirelessVm;
        OperationHistoryVm = historyVm;
        
        // Default
        CurrentCenterViewModel = RouterOperationsVm;
    }

    public RouterOperationsViewModel RouterOperationsVm { get; }
    public ModemOperationsViewModel ModemOperationsVm { get; }
    public WirelessOperationsViewModel WirelessOperationsVm { get; }
    public OperationHistoryViewModel OperationHistoryVm { get; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SwitchCenter(string centerName)
    {
        CurrentCenterViewModel = centerName switch
        {
            "Router" => RouterOperationsVm,
            "Modem" => ModemOperationsVm,
            "Wireless" => WirelessOperationsVm,
            "History" => OperationHistoryVm,
            _ => RouterOperationsVm
        };
    }
}
