using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MikroTikVoucherPrinter.UI.Converters;

/// <summary>
/// Converts a numeric value to a uniform Thickness (Left/Top/Right/Bottom all equal).
/// Optional parameter: multiplier (e.g. 2 => value*2).
/// </summary>
public sealed class DoubleToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return new Thickness(0);

        double v = ParseDouble(value);
        double multiplier = 1.0;
        if (parameter != null)
            multiplier = ParseDouble(parameter);

        var t = Math.Max(0, v * multiplier);
        return new Thickness(t);
    }

    private double ParseDouble(object val)
    {
        if (val == null) return 0.0;
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is int i) return i;
        
        string str = val.ToString()!;
        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double res))
            return res;
        if (double.TryParse(str, NumberStyles.Any, CultureInfo.CurrentCulture, out res))
            return res;
            
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
