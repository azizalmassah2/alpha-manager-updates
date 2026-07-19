using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.Modules.Operations.ViewModels
{
    public partial class OperationsCenterViewModel : ViewModelBase
    {
        public OperationsCenterViewModel(Lux.Management.Console.Core.IPermissionService permissionService, IEventBus eventBus) : base(permissionService, eventBus)
        {
            Title = "OperationsCenter";
        }
    }
}
