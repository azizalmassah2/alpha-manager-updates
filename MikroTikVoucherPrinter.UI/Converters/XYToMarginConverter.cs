using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MikroTikVoucherPrinter.UI.Converters;

public sealed class XYToMarginConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return new Thickness(0);
        
        double x = ParseDouble(values[0]);
        double y = ParseDouble(values[1]);
            
        double multiplier = 1.0;
        if (parameter != null)
            multiplier = ParseDouble(parameter);
            
        return new Thickness(x * multiplier / 2, y * multiplier / 2, x * multiplier / 2, y * multiplier / 2);
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

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
