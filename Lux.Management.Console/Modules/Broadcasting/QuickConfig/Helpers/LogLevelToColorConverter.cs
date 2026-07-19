using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers
{
    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                var color = level switch
                {
                    LogLevel.Info => (Color)ColorConverter.ConvertFromString("#24292f"),
                    LogLevel.Success => (Color)ColorConverter.ConvertFromString("#1a7f37"),
                    LogLevel.Warning => (Color)ColorConverter.ConvertFromString("#9a6700"),
                    LogLevel.Error => (Color)ColorConverter.ConvertFromString("#cf222e"),
                    LogLevel.Ubus => (Color)ColorConverter.ConvertFromString("#0077b6"),
                    _ => (Color)ColorConverter.ConvertFromString("#24292f")
                };
                return new SolidColorBrush(color);
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#24292f"));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
