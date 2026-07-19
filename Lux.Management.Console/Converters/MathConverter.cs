using System;
using System.Globalization;
using System.Windows.Data;

namespace Lux.Management.Console.Converters;

/// <summary>
/// A converter that performs simple math operations on the bound value.
/// Example parameter: "@VALUE*4" multiplies the bound value by 4.
/// </summary>
public class MathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return 0.0;

        string expression = parameter.ToString()!;
        double val;
        
        try
        {
            val = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0.0;
        }

        if (expression.StartsWith("@VALUE*"))
        {
            if (double.TryParse(expression.Substring(7), NumberStyles.Any, CultureInfo.InvariantCulture, out double multiplier))
                return val * multiplier;
        }
        else if (expression.StartsWith("@VALUE/"))
        {
            if (double.TryParse(expression.Substring(7), NumberStyles.Any, CultureInfo.InvariantCulture, out double divider) && divider != 0)
                return val / divider;
        }
        else if (expression.StartsWith("@VALUE+"))
        {
            if (double.TryParse(expression.Substring(7), NumberStyles.Any, CultureInfo.InvariantCulture, out double add))
                return val + add;
        }
        else if (expression.StartsWith("@VALUE-"))
        {
            if (double.TryParse(expression.Substring(7), NumberStyles.Any, CultureInfo.InvariantCulture, out double sub))
                return val - sub;
        }

        return val;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
