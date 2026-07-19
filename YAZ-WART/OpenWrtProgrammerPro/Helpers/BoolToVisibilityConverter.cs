using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenWrtProgrammerPro.Helpers
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                var target = Invert ? !boolValue : boolValue;
                return target ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                var boolVal = visibility == Visibility.Visible;
                return Invert ? !boolVal : boolVal;
            }
            return false;
        }
    }
}
