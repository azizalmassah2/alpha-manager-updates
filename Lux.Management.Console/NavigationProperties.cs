using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Lux.Management.Console;

public static class NavigationItemProperties
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(NavigationItemProperties), new PropertyMetadata(false));

    public static bool GetIsActive(DependencyObject obj) => (bool)obj.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject obj, bool value) => obj.SetValue(IsActiveProperty, value);

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.RegisterAttached("IconData", typeof(Geometry), typeof(NavigationItemProperties), new PropertyMetadata(null));

    public static Geometry GetIconData(DependencyObject obj) => (Geometry)obj.GetValue(IconDataProperty);
    public static void SetIconData(DependencyObject obj, Geometry value) => obj.SetValue(IconDataProperty, value);
}

public class TypeToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        string targetTypeName = parameter.ToString() ?? "";
        string valueTypeName = value.GetType().Name;

        if (valueTypeName.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase))
            return true;

        // إذا كان الـ ViewModel عبارة عن حاوية مثل MikroTikCenterViewModel، افحص الـ CurrentSubPageViewModel داخله
        var prop = value.GetType().GetProperty("CurrentSubPageViewModel");
        if (prop != null)
        {
            var subValue = prop.GetValue(value);
            if (subValue != null && subValue.GetType().Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
