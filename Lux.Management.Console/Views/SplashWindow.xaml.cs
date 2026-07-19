using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;

namespace Lux.Management.Console.Views;

/// <summary>
/// شاشة الانتظار الجميلة — تظهر فور تشغيل البرنامج وتُغلق بعد اكتمال التهيئة
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        // عرض رقم الإصدار من Assembly
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
    }

    /// <summary>
    /// تحديث رسالة الحالة وشريط التقدم من أي خيط بأمان
    /// </summary>
    /// <param name="message">النص المعروض للمستخدم</param>
    /// <param name="step">الخطوة الحالية (1-5)</param>
    public void UpdateStatus(string message, int step)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;

            // حركة سلسة لشريط التقدم
            var animation = new DoubleAnimation
            {
                To = step,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBar.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, animation);
        });
    }
}
