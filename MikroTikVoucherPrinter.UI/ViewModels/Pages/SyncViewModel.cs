using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

public partial class SyncViewModel : BaseViewModel
{
    private readonly ISyncService _syncService;

    public SyncViewModel(ISyncService syncService, ILogger<SyncViewModel> logger) : base(logger)
    {
        _syncService = syncService;
        Title = "المزامنة الحية (Live MikroTik Sync)";

        StartSyncCommand = new AsyncRelayCommand(StartSyncAsync);
    }

    private string _metricsOutput = "الخدمة في وضع السكون الأوتوماتيكي...";
    public string MetricsOutput { get => _metricsOutput; set => SetProperty(ref _metricsOutput, value); }

    public IAsyncRelayCommand StartSyncCommand { get; }

    private async Task StartSyncAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            MetricsOutput = "📡 جاري إنشاء قنوات اتصال بالرواتر الخاص بك...";
            var metrics = await _syncService.ProcessPendingAsync(token);
            MetricsOutput = $"✅ تمت المزامنة بنجاح!\n\nالإحصائيات المباشرة:\n{metrics.ToString()}";
        }, "جاري ضخ الكروت نحو المايكروتك بنظام آمن...");
    }
}
