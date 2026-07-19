using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Models;

namespace Lux.Management.Console.Views;

/// <summary>
/// نافذة التحديث الاحترافية.
/// تظهر فوق Splash Screen وتنتظر قرار المستخدم قبل فتح النافذة الرئيسية.
///
/// تدعم:
///   - إظهار نوع التحديث بشارة ملوّنة
///   - إخفاء زر "لاحقاً" عند التحديث الإجباري
///   - عرض رسالة إدارية
///   - عرض ملاحظات الإصدار كنقاط
///   - عرض تاريخ الإصدار وحجم الملف
///   - شريط تقدم التنزيل
/// </summary>
public partial class UpdateDialog : Window
{
    private readonly UpdateInfo    _update;
    private readonly IUpdateService _updateService;
    private readonly bool          _isMandatory;

    /// <summary>True إذا اختار المستخدم التحديث (أو بدأ التنزيل)</summary>
    public bool UserChoseUpdate { get; private set; }

    public UpdateDialog(UpdateCheckResult result, IUpdateService updateService)
    {
        InitializeComponent();

        _update        = result.Update!;
        _updateService = updateService;
        _isMandatory   = result.MustUpdate;

        BindData();
    }

    // ── ربط البيانات بالواجهة ────────────────────────────────────────────
    private void BindData()
    {
        // ── إصدارات ────────────────────────────────────────────────────
        var current = Assembly.GetEntryAssembly()?.GetName().Version;
        CurrentVersionText.Text = current != null
            ? $"v{current.Major}.{current.Minor}.{current.Build}"
            : "v1.0.0";

        NewVersionText.Text = $"v{_update.Version}";

        // ── شارة نوع التحديث ───────────────────────────────────────────
        TypeBadgeText.Text = _update.UpdateTypeLabel;
        TypeBadge.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(_update.UpdateTypeBadgeColor));

        // ── عنوان الحوار حسب نوع التحديث ──────────────────────────────
        TitleText.Text = _update.UpdateType switch
        {
            UpdateType.Security    => "🔒 تحديث أمني",
            UpdateType.Mandatory   => "⚠️ تحديث إجباري",
            UpdateType.Recommended => "⭐ تحديث موصى به",
            _                      => "🔄 تحديث متاح"
        };

        // ── الرسالة الإدارية ────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(_update.Message))
        {
            AdminMessageText.Text    = _update.Message;
            AdminMessagePanel.Visibility = Visibility.Visible;
        }

        // ── ملاحظات الإصدار (كنقاط) ────────────────────────────────────
        if (_update.ReleaseNotes.Count > 0)
            ReleaseNotesList.ItemsSource = _update.ReleaseNotes;
        else
            ReleaseNotesList.ItemsSource = new[] { "لا توجد تفاصيل لهذا الإصدار." };

        // ── تاريخ الإصدار ───────────────────────────────────────────────
        ReleaseDateText.Text = string.IsNullOrWhiteSpace(_update.ReleaseDate)
            ? "غير محدد"
            : _update.ReleaseDate;

        // ── حجم الملف ──────────────────────────────────────────────────
        FileSizeText.Text = _update.FileSizeFormatted;

        // ── التحديث الإجباري: تعديل زر "لاحقاً" ليصبح "إغلاق البرنامج" ────────
        if (_isMandatory)
        {
            SkipButton.Content = "❌ إغلاق البرنامج";
        }
    }

    // ── زر "لاحقاً" / "إغلاق البرنامج" ──────────────────────────────────────
    private void OnSkipClicked(object sender, RoutedEventArgs e)
    {
        if (_isMandatory)
        {
            Application.Current.Shutdown();
        }
        else
        {
            UserChoseUpdate = false;
            Close();
        }
    }

    // ── زر "تحديث الآن" ───────────────────────────────────────────────────
    private async void OnUpdateClicked(object sender, RoutedEventArgs e)
    {
        UserChoseUpdate    = true;
        UpdateButton.IsEnabled = false;
        SkipButton.IsEnabled   = false;
        ProgressPanel.Visibility = Visibility.Visible;
        UpdateButton.Content = "⏳  جارٍ التنزيل...";

        var progress = new Progress<int>(pct => Dispatcher.Invoke(() =>
        {
            DownloadPctText.Text    = $"{pct}%";
            DownloadStatusText.Text = $"جاري التنزيل... {pct}%";

            // حساب عرض شريط التقدم بالنسبة لعرض الـ Grid
            if (ProgressFill.Parent is FrameworkElement parent)
                ProgressFill.Width = parent.ActualWidth * pct / 100.0;
        }));

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            // DownloadAndInstallAsync تُغلق البرنامج تلقائياً — لن نصل للأسطر التالية
            await _updateService.DownloadAndInstallAsync(_update, progress, cts.Token);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"فشل تنزيل التحديث:\n{ex.Message}\n\nيرجى التحقق من اتصال الإنترنت والمحاولة مرة أخرى.",
                "خطأ في التنزيل",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            if (_isMandatory)
            {
                // إذا كان التحديث إجبارياً: نعيد تهيئة الأزرار لإعادة المحاولة ولا نغلق النافذة
                UpdateButton.IsEnabled = true;
                SkipButton.IsEnabled   = true;
                UpdateButton.Content   = "⬇️  إعادة محاولة التنزيل";
                ProgressPanel.Visibility = Visibility.Collapsed;
                UserChoseUpdate = false;
            }
            else
            {
                // إذا كان اختيارياً: نغلق النافذة ونتابع للبرنامج
                UserChoseUpdate = false;
                Close();
            }
        }
    }
}
