using System;
using System.Collections.Generic;

namespace Lux.Management.Console.Core;

public enum UserRole
{
    Viewer,
    Operator,
    Administrator
}

public interface IUserContext
{
    string Username { get; }
    UserRole Role { get; }
}
