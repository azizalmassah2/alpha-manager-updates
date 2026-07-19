using System;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Services;

public class MockPermissionService : IPermissionService
{
    private readonly IUserContext _userContext;

    public MockPermissionService(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public bool CanViewDevices() => true;

    public bool CanProvisionDevices() => _userContext.Role is UserRole.Administrator or UserRole.Operator;

    public bool CanConfigureDevices() => _userContext.Role is UserRole.Administrator or UserRole.Operator;

    public bool CanManageFirmware() => _userContext.Role == UserRole.Administrator;

    public bool CanExecuteFleetOperations() => _userContext.Role == UserRole.Administrator;

    public bool HasFullAccess() => _userContext.Role == UserRole.Administrator;
}

public class MockUserContext : IUserContext
{
    public string Username => "AdminUser";
    public UserRole Role => UserRole.Administrator;
}
