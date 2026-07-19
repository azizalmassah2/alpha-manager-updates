using System;
using System.Globalization;
using System.Windows.Data;

namespace Lux.Management.Console.Converters;

public class CardSizeConverter : IMultiValueConverter
{
    // values: [0]=TotalSize (210 for width, 297 for height), [1]=Count (Cols or Rows), [2]=Margin
    // parameter: multiplier (e.g. 3.77 for pixels)
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3) return 100.0;
        
        double totalSize = ParseDouble(values[0]);
        int count = (int)ParseDouble(values[1]);
        double margin = ParseDouble(values[2]);
        
        if (count <= 0) return 100.0;
        
        double multiplier = 1.0;
        if (parameter != null) multiplier = ParseDouble(parameter);
        
        double cardSize = (totalSize - (margin * count)) / count;
        if (cardSize < 1) cardSize = 1;
        
        return cardSize * multiplier;
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
