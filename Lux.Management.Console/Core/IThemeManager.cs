using System;

namespace Lux.Management.Console.Core;

public interface IThemeManager
{
    void SetLightTheme();
    void SetDarkTheme();
    void ToggleTheme();
}
