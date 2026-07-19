using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.Views;

public class ScreenStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScreenStatus status)
        {
            var color = status switch
            {
                ScreenStatus.Loading => Color.FromRgb(43, 108, 176),       // أزرق هادئ
                ScreenStatus.Connecting => Color.FromRgb(43, 108, 176),    // أزرق
                ScreenStatus.Syncing => Color.FromRgb(217, 119, 6),        // برتقالي/ذهبي هادئ
                ScreenStatus.Offline => Color.FromRgb(217, 119, 6),        // برتقالي ثابت
                ScreenStatus.PendingChanges => Color.FromRgb(217, 119, 6), // برتقالي نابض
                ScreenStatus.Updated => Color.FromRgb(16, 124, 65),        // أخضر هادئ
                ScreenStatus.Failed => Color.FromRgb(197, 59, 42),         // أحمر ناصع
                ScreenStatus.ImportingLegacyVouchers => Color.FromRgb(43, 108, 176), // أزرق هادئ
                _ => Color.FromRgb(128, 128, 128)
            };
            return new SolidColorBrush(color);
        }
        return new SolidColorBrush(Color.FromRgb(128, 128, 128));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
