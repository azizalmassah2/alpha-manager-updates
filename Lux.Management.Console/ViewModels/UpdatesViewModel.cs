using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Models;

namespace Lux.Management.Console.ViewModels;

public partial class UpdatesViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;
    private readonly IUserNotificationService _notificationService;

    private UpdateCheckResult? _latestUpdateResult;

    [ObservableProperty]
    private string _currentVersion = "v1.0.0";

    [ObservableProperty]
    private string _statusMessage = "برنامجك محدث بالكامل ✓";

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private string _updateVersion = string.Empty;

    [ObservableProperty]
    private string _changelog = string.Empty;

    public UpdatesViewModel(
        IPermissionService permissionService,
        IEventBus eventBus,
        IUpdateService updateService,
        IUserNotificationService notificationService)
        : base(permissionService, eventBus)
    {
        _updateService = updateService;
        _notificationService = notificationService;
        Title = "التحديثات";

        var current = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        CurrentVersion = current != null ? $"v{current.Major}.{current.Minor}.{current.Build}" : "v1.0.0";
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        IsChecking = true;
        StatusMessage = "جاري فحص التحديثات...";
        HasUpdate = false;
        _latestUpdateResult = null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await _updateService.CheckForUpdateAsync(cts.Token);
            _latestUpdateResult = result;

            if (result.HasUpdate)
            {
                var update = result.Update!;
                HasUpdate = true;
                UpdateVersion = $"v{update.Version} ({update.UpdateTypeLabel})";
                Changelog = update.ReleaseNotes.Count == 0 
                    ? "لا توجد تفاصيل لهذا الإصدار." 
                    : string.Join(Environment.NewLine, update.ReleaseNotes);

                if (!string.IsNullOrEmpty(update.Message))
                {
                    Changelog = $"📢 {update.Message}{Environment.NewLine}{Environment.NewLine}{Changelog}";
                }

                StatusMessage = $"⚠️ يتوفر إصدار جديد: {update.Version}";
                _notificationService.ShowInformation($"إصدار جديد متوفر: {update.Version}");

                // عرض نافذة التحديث الاحترافية فوراً
                var updateDialog = new Views.UpdateDialog(result, _updateService);
                updateDialog.ShowDialog();
            }
            else
            {
                HasUpdate = false;
                StatusMessage = "أنت تستخدم الإصدار الأخير بالفعل. برنامجك محدث ✓";
                _notificationService.ShowSuccess("البرنامج محدث بالكامل!");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ فشل فحص التحديثات: {ex.Message}";
            _notificationService.ShowError("فشل الاتصال بخادم التحديثات.");
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void DownloadUpdate()
    {
        if (_latestUpdateResult != null && _latestUpdateResult.HasUpdate)
        {
            var updateDialog = new Views.UpdateDialog(_latestUpdateResult, _updateService);
            updateDialog.ShowDialog();
        }
    }
}
