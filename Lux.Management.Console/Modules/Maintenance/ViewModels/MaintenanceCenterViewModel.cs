using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Infrastructure.Services;

namespace Lux.Management.Console.Modules.Maintenance.ViewModels;

public partial class MaintenanceCenterViewModel : ViewModelBase
{
    private readonly MaintenanceService _maintenanceService;
    private readonly ILogger<MaintenanceCenterViewModel> _logger;

    [ObservableProperty]
    private string _statusMessage = "جاهز لتنفيذ الصيانة";

    [ObservableProperty]
    private bool _isBusy;

    // ── Router Maintenance Properties ──────────────────────────────────
    [ObservableProperty]
    private bool _isCleanQuotaSelected = true;

    [ObservableProperty]
    private bool _isCleanTimeSelected;

    [ObservableProperty]
    private bool _isCleanSessionsSelected;

    [ObservableProperty]
    private string _selectedInterval = "1d"; // Default: 1d (يومياً)

    public ObservableCollection<string> IntervalOptions { get; } = new()
    {
        "1d",  // يومياً
        "12h", // كل 12 ساعة
        "6h",  // كل 6 ساعات
        "1h",  // كل ساعة
        "2d",  // كل يومين
        "7d"   // أسبوعياً
    };

    // ── SQLite DB Maintenance Options ─────────────────────────────────
    [ObservableProperty]
    private bool _cleanDbLogs = true;

    [ObservableProperty]
    private bool _cleanDbSessions = true;

    [ObservableProperty]
    private bool _vacuumDb = true;

    public MaintenanceCenterViewModel(
        IPermissionService permissionService,
        IEventBus eventBus,
        MaintenanceService maintenanceService,
        ILogger<MaintenanceCenterViewModel> logger)
        : base(permissionService, eventBus)
    {
        Title = "مركز الصيانة والجدولة";
        _maintenanceService = maintenanceService;
        _logger = logger;
    }

    private MaintenanceScriptType GetSelectedScriptType(out string scriptTitle)
    {
        if (IsCleanTimeSelected)
        {
            scriptTitle = "تنظيف كروت الوقت المنتهي";
            return MaintenanceScriptType.TimeCleanup;
        }
        if (IsCleanSessionsSelected)
        {
            scriptTitle = "تنظيف الجلسات واللوج";
            return MaintenanceScriptType.SessionsCleanup;
        }

        scriptTitle = "تنظيف كروت الرصيد المستنفدة";
        return MaintenanceScriptType.QuotaCleanup;
    }

    [RelayCommand]
    private async Task ExecuteRouterScriptImmediatelyAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        var type = GetSelectedScriptType(out var scriptTitle);

        StatusMessage = $"جاري تنفيذ عملية ({scriptTitle}) فورياً على الراوتر...";

        try
        {
            var result = await _maintenanceService.ExecuteRouterScriptImmediatelyAsync(type);

            if (result.IsSuccess)
            {
                string details = result.Value;
                StatusMessage = $"✅ تم تنفيذ عملية ({scriptTitle}) بنجاح على الراوتر!";
                System.Windows.MessageBox.Show(
                    $"✅ تمت عملية الصيانة الفورية ({scriptTitle}) بنجاح!\n\n{details}",
                    "نجاح التنفيذ الفوري",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"❌ فشل التنفيذ الفوري: {result.ErrorMessage}";
                System.Windows.MessageBox.Show(
                    $"فشلت عملية الصيانة الفورية على الراوتر:\n{result.ErrorMessage}",
                    "خطأ في التنفيذ",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing immediate router script");
            StatusMessage = $"❌ خطأ: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ScheduleRouterScriptAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        var type = GetSelectedScriptType(out var scriptTitle);

        StatusMessage = "جاري إرسال وجدولة الاسكريبت على الراوتر...";

        try
        {
            var result = await _maintenanceService.ScheduleRouterScriptAsync(type, SelectedInterval);

            if (result.IsSuccess)
            {
                StatusMessage = $"✅ تم إرسال وتفعيل جدولة ({scriptTitle}) بتكرار ({SelectedInterval}) بنجاح على الراوتر!";
                System.Windows.MessageBox.Show(
                    $"تم رفع الاسكريبت وجدولته بنجاح على الراوتر.\nالجدولة: {SelectedInterval}",
                    "نجاح الجدولة",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"❌ فشل الجدولة: {result.ErrorMessage}";
                System.Windows.MessageBox.Show(
                    $"فشلت عملية الجدولة على الراوتر:\n{result.ErrorMessage}",
                    "خطأ في الجدولة",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling router script");
            StatusMessage = $"❌ غير متوقع: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RebuildDatabaseAsync()
    {
        if (IsBusy) return;

        var confirm = System.Windows.MessageBox.Show(
            "هل أنت تأكد من رغبتك في صيانة وإعادة بناء قاعدة البيانات المحلية (SQLite)؟\nسيتم تطبيق الخيارات المحددة (حذف السجلات، تنظيف الجلسات، وتقليص حجم الملف).",
            "تأكيد صيانة قاعدة البيانات",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusMessage = "جاري صيانة وإعادة بناء قاعدة البيانات المحلية...";

        try
        {
            var result = await _maintenanceService.RebuildDatabaseAsync(CleanDbLogs, CleanDbSessions, VacuumDb);
            if (result.IsSuccess)
            {
                string details = result.Value;
                StatusMessage = $"✅ تمت صيانة قاعدة البيانات بنجاح!";
                System.Windows.MessageBox.Show(
                    $"✅ تمت عملية صيانة قاعدة البيانات بنجاح!\n\n{details}",
                    "نجاح الصيانة",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"❌ فشل الصيانة: {result.ErrorMessage}";
                System.Windows.MessageBox.Show(
                    $"فشلت صيانة قاعدة البيانات:\n{result.ErrorMessage}",
                    "خطأ في قاعدة البيانات",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in database rebuild command");
            StatusMessage = $"❌ خطأ: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RebootRouterAsync()
    {
        if (IsBusy) return;

        var confirm = System.Windows.MessageBox.Show(
            "⚠️ تحذير: هل أنت متأكد من رغبتك في إعادة تشغيل راوتر المايكروتيك (/system/reboot) الآن؟\nسيؤدي ذلك لانقطاع الاتصال مؤقتاً عن المشتركين حتى مكتمل إقلاع الراوتر.",
            "تأكيد إعادة تشغيل الراوتر",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusMessage = "جاري إرسال أمر إعادة تشغيل الراوتر (/system/reboot)...";

        try
        {
            var result = await _maintenanceService.RebootRouterAsync();
            if (result.IsSuccess)
            {
                StatusMessage = "✅ تم إرسال أمر إعادة تشغيل الراوتر بنجاح!";
                System.Windows.MessageBox.Show(
                    "تم إرسال أمر إعادة تشغيل الراوتر بنجاح.\nقد يستغرق الراوتر دقيقة لإعادة الإقلاع والاتصال مجدداً.",
                    "إعادة تشغيل الراوتر",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"❌ فشل إرسال الأمر: {result.ErrorMessage}";
                System.Windows.MessageBox.Show(
                    $"فشل إرسال أمر إعادة التشغيل:\n{result.ErrorMessage}",
                    "خطأ بالراوتر",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebooting router");
            StatusMessage = $"❌ خطأ: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
