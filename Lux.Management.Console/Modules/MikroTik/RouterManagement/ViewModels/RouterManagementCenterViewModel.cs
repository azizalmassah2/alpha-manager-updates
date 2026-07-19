using CommunityToolkit.Mvvm.ComponentModel;
using Lux.Management.Console.Modules.MikroTik.Backups.ViewModels;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

public partial class RouterManagementCenterViewModel : ObservableObject
{
    public RouterDashboardViewModel DashboardVM { get; }
    public SystemResourcesViewModel ResourcesVM { get; }
    public InterfacesViewModel InterfacesVM { get; }
    public IpAddressesViewModel IpAddressesVM { get; }
    public RoutesViewModel RoutesVM { get; }
    public DnsViewModel DnsVM { get; }
    public NtpViewModel NtpVM { get; }
    public BackupsViewModel BackupsVM { get; }
    public RouterOperationsViewModel OperationsVM { get; }
    public NocViewModel NocVM { get; }

    public RouterManagementCenterViewModel(
        RouterDashboardViewModel dashboardVM,
        SystemResourcesViewModel resourcesVM,
        InterfacesViewModel interfacesVM,
        IpAddressesViewModel ipAddressesVM,
        RoutesViewModel routesVM,
        DnsViewModel dnsVM,
        NtpViewModel ntpVM,
        BackupsViewModel backupsVM,
        RouterOperationsViewModel operationsVM,
        NocViewModel nocVM)
    {
        DashboardVM = dashboardVM;
        ResourcesVM = resourcesVM;
        InterfacesVM = interfacesVM;
        IpAddressesVM = ipAddressesVM;
        RoutesVM = routesVM;
        DnsVM = dnsVM;
        NtpVM = ntpVM;
        BackupsVM = backupsVM;
        OperationsVM = operationsVM;
        NocVM = nocVM;
    }
}
