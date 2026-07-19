using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

public partial class PrintCenterViewModel : BaseViewModel
{
    private readonly IVoucherQueryService _queryService;
    private readonly IPrintService        _printService;
    private readonly ITemplateService       _templateService;
    private readonly ISettingsService     _settingsService;
    private bool _suppressTemplatePersist;

    // ═══════════════════════════════════════════════════
    //  إعدادات الطباعة (ترتبط مباشرة بـ XAML)
    // ═══════════════════════════════════════════════════
    public PrintSettingsDto Settings { get; } = new PrintSettingsDto();

    // ═══════════════════════════════════════════════════
    //  كروت الطباعة
    // ═══════════════════════════════════════════════════
    public ObservableCollection<VoucherDto> PrintVouchers { get; } = new();
    public ICollectionView PrintVouchersView { get; }

    // ═══════════════════════════════════════════════════
    //  الباقات المتاحة للفلترة
    // ═══════════════════════════════════════════════════
    public ObservableCollection<string> AvailableProfiles { get; } = new() { "كل الباقات" };

    public ObservableCollection<TemplateConfigDto> AvailablePrintTemplates { get; } = new();

    private TemplateConfigDto? _selectedPrintTemplate;
    public TemplateConfigDto? SelectedPrintTemplate
    {
        get => _selectedPrintTemplate;
        set
        {
            if (SetProperty(ref _selectedPrintTemplate, value))
            {
                ApplyPrintTemplateToSettings();
                OnPropertyChanged(nameof(SelectedTemplateCaption));
                OnPropertyChanged(nameof(TemplateLivePreviewLine));
                if (!_suppressTemplatePersist)
                    _ = PersistCenterTemplateAsync();
            }
        }
    }

    public string SelectedTemplateCaption =>
        SelectedPrintTemplate?.Name ?? Settings.TemplateName;

    public string TemplateLivePreviewLine =>
        SelectedPrintTemplate == null
            ? ""
            : $"{SelectedPrintTemplate.KindDisplay} · {SelectedPrintTemplate.GridSummary} · أعمدة {SelectedPrintTemplate.Columns} × صفوف {SelectedPrintTemplate.Rows}";

    public PrintCenterViewModel(
        IVoucherQueryService queryService,
        IPrintService        printService,
        ITemplateService      templateService,
        ISettingsService    settingsService,
        ILogger<PrintCenterViewModel> logger) : base(logger)
    {
        _queryService = queryService;
        _printService  = printService;
        _templateService = templateService;
        _settingsService = settingsService;
        Title = "مركز الطباعة";

        PrintVouchersView = CollectionViewSource.GetDefaultView(PrintVouchers);
        PrintVouchersView.Filter = FilterPrintVouchers;

        PrintCommand       = new AsyncRelayCommand(PrintAsync,       CanPrint);
        PreviewCommand     = new AsyncRelayCommand(PreviewAsync,     CanPrint);
        LoadAllCommand     = new AsyncRelayCommand(LoadAllAsync);
        LoadPendingCommand = new AsyncRelayCommand(LoadPendingAsync);
        BrowseLogoCommand  = new RelayCommand(BrowseLogo);
    }

    // ═══════════════════════════════════════════════════
    //  فلاتر الطباعة
    // ═══════════════════════════════════════════════════
    private string _printSearchText = "";
    public string PrintSearchText
    {
        get => _printSearchText;
        set { SetProperty(ref _printSearchText, value); PrintVouchersView.Refresh(); }
    }

    private string _filterProfile = "كل الباقات";
    public string FilterProfile
    {
        get => _filterProfile;
        set { SetProperty(ref _filterProfile, value); PrintVouchersView.Refresh(); }
    }

    private bool FilterPrintVouchers(object obj)
    {
        if (obj is not VoucherDto v) return false;

        if (!string.IsNullOrWhiteSpace(PrintSearchText))
        {
            if (!v.Username.Contains(PrintSearchText, StringComparison.OrdinalIgnoreCase) &&
                !v.Profile.Contains(PrintSearchText, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (FilterProfile != "كل الباقات" && v.Profile != FilterProfile)
            return false;

        return true;
    }

    // ═══════════════════════════════════════════════════
    //  رسالة الحالة المخصصة للطباعة
    // ═══════════════════════════════════════════════════
    private string _printStatusMessage = "";
    public string PrintStatusMessage
    {
        get => _printStatusMessage;
        set => SetProperty(ref _printStatusMessage, value);
    }

    // ═══════════════════════════════════════════════════
    //  الأوامر
    // ═══════════════════════════════════════════════════
    public IAsyncRelayCommand PrintCommand       { get; }
    public IAsyncRelayCommand PreviewCommand     { get; }
    public IAsyncRelayCommand LoadAllCommand     { get; }
    public IAsyncRelayCommand LoadPendingCommand { get; }
    public IRelayCommand      BrowseLogoCommand  { get; }

    private bool CanPrint() => PrintVouchers.Count > 0;

    // ═══════════════════════════════════════════════════
    //  تحميل الكروت
    // ═══════════════════════════════════════════════════
    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadPrintTemplatesAsync();
        await LoadAllAsync();
    }

    private async Task LoadPrintTemplatesAsync()
    {
        var list = await _templateService.GetTemplatesAsync();
        var lastRaw = _settingsService.Get("Print.LastCenterTemplateId", "");
        _ = Guid.TryParse(lastRaw, out var lastGuid);

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _suppressTemplatePersist = true;
            try
            {
                AvailablePrintTemplates.Clear();
                foreach (var t in list)
                    AvailablePrintTemplates.Add(t);

                SelectedPrintTemplate = AvailablePrintTemplates.FirstOrDefault(x => x.Id == lastGuid)
                    ?? AvailablePrintTemplates.FirstOrDefault();
            }
            finally
            {
                _suppressTemplatePersist = false;
            }
        });
    }

    private void ApplyPrintTemplateToSettings()
    {
        if (SelectedPrintTemplate != null)
            Settings.CustomTemplateId = SelectedPrintTemplate.Id;
        else
            Settings.CustomTemplateId = null;

        OnPropertyChanged(nameof(Settings));
    }

    private async Task PersistCenterTemplateAsync()
    {
        try
        {
            if (SelectedPrintTemplate != null)
                _settingsService.Set("Print.LastCenterTemplateId", SelectedPrintTemplate.Id.ToString());
            else
                _settingsService.Set("Print.LastCenterTemplateId", string.Empty);
            await _settingsService.SaveAsync();
        }
        catch { /* ignore */ }
    }

    private async Task LoadAllAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var data = await _queryService.GetAllVouchersAsync(token);
            LoadVouchersIntoCollection(data);
            PrintStatusMessage = $"تم تحميل {data.Count} كرت";
        }, "جاري تحميل جميع الكروت...");

        NotifyPrintCommands();
    }

    private async Task LoadPendingAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var data = await _queryService.GetPendingSyncVouchersProjectedAsync(token);
            LoadVouchersIntoCollection(data);
            PrintStatusMessage = $"تم تحميل {data.Count} كرت غير مطبوع";
        }, "جاري تحميل الكروت غير المطبوعة...");

        NotifyPrintCommands();
    }

    private void LoadVouchersIntoCollection(IReadOnlyList<VoucherDto> data)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            PrintVouchers.Clear();
            foreach (var d in data) PrintVouchers.Add(d);

            // تحديث قائمة الباقات للفلتر
            AvailableProfiles.Clear();
            AvailableProfiles.Add("كل الباقات");
            foreach (var profile in data.Select(x => x.Profile).Distinct().OrderBy(x => x))
                AvailableProfiles.Add(profile);

            PrintVouchersView.Refresh();
        });
    }

    private void NotifyPrintCommands()
    {
        (PrintCommand   as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (PreviewCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    // ═══════════════════════════════════════════════════
    //  توليد PDF (معاينة)
    // ═══════════════════════════════════════════════════
    private async Task PreviewAsync()
    {
        string tempFile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"luxcard_preview_{DateTime.Now:HHmmss}.pdf");
            
        await GeneratePdfCore(tempFile, openAfter: true);
    }

    // ═══════════════════════════════════════════════════
    //  طباعة مباشرة / تصدير
    // ═══════════════════════════════════════════════════
    private async Task PrintAsync()
    {
        string? savePath = null;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "اختر مسار حفظ ملف الطباعة (PDF)",
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = $"LuxCard_Print_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };
            if (dlg.ShowDialog() == true)
                savePath = dlg.FileName;
        });

        if (savePath == null) return; // المستخدم ألغى الحفظ

        await GeneratePdfCore(savePath, openAfter: true);
    }

    private async Task GeneratePdfCore(string filePath, bool openAfter)
    {
        var visible = PrintVouchersView.Cast<VoucherDto>().ToList();
        if (visible.Count == 0) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var result = await _printService.GeneratePdfAsync(
                new List<VoucherDto>(visible), Settings, token);

            if (result.IsSuccess)
            {
                System.IO.File.WriteAllBytes(filePath, result.Value);

                if (openAfter)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
                }

                PrintStatusMessage = $"✅ تم توليد PDF لـ {visible.Count} كرت بنجاح";
                Logger.LogInformation("PDF جاهز: {File}", filePath);
            }
            else
            {
                PrintStatusMessage = $"❌ فشل التوليد: {result.ErrorMessage}";
                Logger.LogError("فشل PDF: {Err}", result.ErrorMessage);
            }

        }, $"جاري بناء PDF لـ {visible.Count} كرت...");
    }

    // ═══════════════════════════════════════════════════
    //  تصفح اختيار ملف الشعار
    // ═══════════════════════════════════════════════════
    private void BrowseLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "اختر صورة الشعار",
            Filter = "ملفات الصور|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            Settings.CompanyLogoPath = dialog.FileName;
            OnPropertyChanged(nameof(Settings));
        }
    }
}
