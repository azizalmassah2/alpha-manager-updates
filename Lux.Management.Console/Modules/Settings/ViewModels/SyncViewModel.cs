using Lux.Management.Console.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Modules.Settings.ViewModels;

public partial class SyncViewModel : ViewModelBase
{
    public SyncViewModel(IPermissionService permissionService, IEventBus eventBus) : base(permissionService, eventBus)
    {
    }
}
