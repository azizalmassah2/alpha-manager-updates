using System;
using System.Windows;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Themes;

public class ThemeManager : IThemeManager
{
    private const string LightThemeUri = "pack://application:,,,/Lux.Management.Console;component/Themes/LightTheme.xaml";
    private const string DarkThemeUri = "pack://application:,,,/Lux.Management.Console;component/Themes/DarkTheme.xaml";
    
    private bool _isDark = false;

    public void SetLightTheme()
    {
        ApplyTheme(LightThemeUri);
        _isDark = false;
    }

    public void SetDarkTheme()
    {
        ApplyTheme(DarkThemeUri);
        _isDark = true;
    }

    public void ToggleTheme()
    {
        if (_isDark)
            SetLightTheme();
        else
            SetDarkTheme();
    }

    private void ApplyTheme(string uri)
    {
        var newThemeDict = new ResourceDictionary { Source = new Uri(uri, UriKind.Absolute) };
        var mergedDicts = Application.Current.Resources.MergedDictionaries;

        int indexToReplace = -1;
        for (int i = 0; i < mergedDicts.Count; i++)
        {
            var dictUriStr = mergedDicts[i].Source?.ToString() ?? "";
            if (dictUriStr.Contains("Themes/LightTheme.xaml") || dictUriStr.Contains("Themes/DarkTheme.xaml"))
            {
                indexToReplace = i;
                break;
            }
        }

        if (indexToReplace != -1)
        {
            mergedDicts[indexToReplace] = newThemeDict;
        }
        else
        {
            mergedDicts.Add(newThemeDict);
        }
    }
}
