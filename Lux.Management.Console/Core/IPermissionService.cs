using System;

namespace Lux.Management.Console.Core;

public interface IPermissionService
{
    bool CanViewDevices();
    bool CanProvisionDevices();
    bool CanConfigureDevices();
    bool CanManageFirmware();
    bool CanExecuteFleetOperations();
    bool HasFullAccess();
}
