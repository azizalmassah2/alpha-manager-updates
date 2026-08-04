using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;
using Lux.Management.Console.Core;
using Lux.Management.Console.Core.Security.Authorization;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Security.Configuration;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;

public partial class CreateBatchDialogViewModel : ObservableObject
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ISyncService _syncService;
    private readonly IPrintService _printService;
    private readonly ITemplateService _templateService;
    private readonly ISettingsService _settingsService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IShellState _shellState;
    private readonly IFeatureAuthorizationService _featureAuthorizationService;
    private readonly IProfileService _profileService;
    private readonly ILogger _logger;

    private Guid _fallbackSystemTemplateId;
    public event Action<bool>? RequestClose;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPrice))]
    private Profile? _selectedProfile;

    [ObservableProperty]
    private Agent? _selectedAgent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalculatedPagesCount))]
    private PrintTemplatePickOption? _selectedPrintTemplateOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPrice))]
    [NotifyPropertyChangedFor(nameof(CalculatedPagesCount))]
    private int _count = 100;

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPasswordLength))]
    private CredentialMode _selectedCredentialMode = CredentialMode.UsernameOnly;

    [ObservableProperty]
    private CharacterMode _selectedCharacterMode = CharacterMode.DigitsOnly;

    [ObservableProperty]
    private string _prefix = string.Empty;

    [ObservableProperty]
    private int _usernameLength = 6;

    [ObservableProperty]
    private CharacterMode _passwordCharacterMode = CharacterMode.DigitsOnly;

    [ObservableProperty]
    private string _passwordPrefix = string.Empty;

    [ObservableProperty]
    private int _passwordLength = 6;

    [ObservableProperty]
    private bool _autoSyncAfterGenerate = true;

    [ObservableProperty]
    private bool _printAfterGenerate = true;

    [ObservableProperty]
    private string? _customSavePath;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private int _progressCurrent;

    [ObservableProperty]
    private int _progressTotal;

    [ObservableProperty]
    private string _statusMessage = "النظام مستعد للمعالجة";

    [ObservableProperty]
    private string _resultMessage = "";

    [ObservableProperty]
    private bool _hasResult;

    // [FIX-C] حالة الاتصال المرئية داخل الـ Dialog
    [ObservableProperty]
    private string _connectionStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isConnectionLost = false;

    public ObservableCollection<Profile> Profiles { get; } = new();
    public ObservableCollection<Agent> Agents { get; } = new();
    public ObservableCollection<PrintTemplatePickOption> PrintTemplateOptions { get; } = new();

    public decimal TotalPrice => Count * Price;
    public bool ShowPasswordLength => SelectedCredentialMode == CredentialMode.UsernameAndPassword;

    public int CalculatedPagesCount
    {
        get
        {
            int cols = 3;
            int rows = 7;
            var t = SelectedPrintTemplateOption?.Source;
            if (t != null)
            {
                cols = t.Columns > 0 ? t.Columns : 3;
                rows = t.Rows > 0 ? t.Rows : 7;
            }
            int perPage = cols * rows;
            return perPage > 0 ? (int)Math.Ceiling((double)Count / perPage) : 0;
        }
    }

    public CreateBatchDialogViewModel(
        IDbContextFactory<LuxCardDbContext> dbFactory,
        ISyncService syncService,
        IPrintService printService,
        ITemplateService templateService,
        ISettingsService settingsService,
        IActiveRouterContext activeRouterContext,
        IShellState shellState,
        ILogger logger,
        IFeatureAuthorizationService featureAuthorizationService,
        IProfileService profileService)
    {
        _dbFactory = dbFactory;
        _syncService = syncService;
        _printService = printService;
        _templateService = templateService;
        _settingsService = settingsService;
        _activeRouterContext = activeRouterContext;
        _shellState = shellState;
        _featureAuthorizationService = featureAuthorizationService;
        _logger = logger;
        _profileService = profileService;

        GenerateBulkCommand = new AsyncRelayCommand(GenerateBulkAsync, () => !IsGenerating);

        // [FIX-C] الاشتراك في حدث تغيير حالة الاتصال
        _activeRouterContext.ActiveRouterChanged += OnRouterConnectionChanged;
    }

    // [FIX-C] معالج انقطاع/عودة الاتصال أثناء عمل الـ Dialog
    private void OnRouterConnectionChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!_activeRouterContext.IsConnected)
            {
                IsConnectionLost = true;
                ConnectionStatusMessage = "⚠️ انقطع الاتصال بالراوتر!";
                if (IsGenerating)
                {
                    StatusMessage = "⚠️ انقطع الاتصال بالراوتر أثناء المزامنة - الكروت محفوظة محلياً";
                }
            }
            else
            {
                IsConnectionLost = false;
                ConnectionStatusMessage = string.Empty;
            }
        });
    }

    public async Task InitializeAsync()
    {
        try
        {
            var routerId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
            using var db = await _dbFactory.CreateDbContextAsync();

            // Load profiles via IProfileService
            IReadOnlyList<Profile> profilesList = new List<Profile>();
            try
            {
                profilesList = await _profileService.GetAllProfilesAsync(PackageSourceType.UserManager);
                if (profilesList.Count == 0)
                {
                    profilesList = await _profileService.GetAllProfilesAsync(PackageSourceType.Hotspot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch live profiles via ProfileService, falling back to local DB");
            }

            if (profilesList.Count == 0)
            {
                profilesList = await db.Profiles
                    .IgnoreQueryFilters()
                    .Where(p => p.RouterId == routerId || routerId == Guid.Empty)
                    .ToListAsync();
            }

            Profiles.Clear();
            foreach (var p in profilesList)
            {
                Profiles.Add(p);
            }
            SelectedProfile = Profiles.FirstOrDefault();

            // Load agents strictly for current router
            var agentsList = await db.Agents
                .IgnoreQueryFilters()
                .Where(a => a.RouterId == routerId && !a.IsDeleted)
                .ToListAsync();

            Agents.Clear();
            foreach (var a in agentsList)
            {
                Agents.Add(a);
            }

            // Load templates
            _fallbackSystemTemplateId = await _templateService.GetPrimarySystemTemplateIdAsync();
            var templates = await _templateService.GetTemplatesAsync();

            PrintTemplateOptions.Clear();
            PrintTemplateOptions.Add(new PrintTemplatePickOption
            {
                IsProfileDefaultChoice = true,
                Title = "افتراضي الباقة",
                Subtitle = "قالب الباقة — أو القالب النظامي إن لم يُحدّد",
                TemplateId = null,
                ThumbnailPath = null,
                Source = null
            });

            foreach (var dto in templates)
            {
                PrintTemplateOptions.Add(new PrintTemplatePickOption
                {
                    TemplateId = dto.Id,
                    Title = dto.Name,
                    Subtitle = $"{dto.KindDisplay} · {dto.GridSummary}",
                    ThumbnailPath = dto.BackgroundImagePath,
                    Source = dto
                });
            }

            var lastSavedTemplateIdStr = _settingsService.Get("Print.LastGenerateTemplateId", string.Empty);
            Guid? lastGuid = Guid.TryParse(lastSavedTemplateIdStr, out var g) ? g : null;

            SelectedPrintTemplateOption = PrintTemplateOptions.FirstOrDefault(o => o.TemplateId == lastGuid)
                ?? PrintTemplateOptions.FirstOrDefault(o => o.IsProfileDefaultChoice)
                ?? PrintTemplateOptions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize create batch dialog data");
        }
    }

    partial void OnSelectedProfileChanged(Profile? value)
    {
        if (value != null)
        {
            Price = value.Price;
            AutoSelectTemplateForProfile(value);
        }
    }

    private void AutoSelectTemplateForProfile(Profile profile)
    {
        if (profile.TemplateId.HasValue && profile.TemplateId.Value != Guid.Empty)
        {
            var match = PrintTemplateOptions.FirstOrDefault(o => o.Source?.Id == profile.TemplateId.Value);
            if (match != null)
            {
                SelectedPrintTemplateOption = match;
                return;
            }
        }
        var defaultOpt = PrintTemplateOptions.FirstOrDefault(o => o.IsProfileDefaultChoice);
        if (defaultOpt != null) SelectedPrintTemplateOption = defaultOpt;
    }

    [RelayCommand]
    private void BrowseCustomSavePath()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "اختر مسار حفظ ملف PDF الطباعة",
            Filter = "ملفات PDF (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName = $"LuxCard_Batch_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
        };
        if (dlg.ShowDialog() == true)
        {
            CustomSavePath = dlg.FileName;
        }
    }

    public IAsyncRelayCommand GenerateBulkCommand { get; }

    private const string DIGITS = "0123456789";
    private const string DIGITS_SAFE = "23456789"; // بدون 0,1 لتجنب اللبس
    private const string LETTERS_UPPER = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // بدون I,O
    private const string LETTERS_LOWER = "abcdefghjkmnpqrstuvwxyz"; // بدون i,l,o
    private const string MIXED = LETTERS_UPPER + DIGITS_SAFE;
    private const string LOWERCASE_MIXED = LETTERS_LOWER + DIGITS_SAFE;

    private static string PoolForCharacterMode(CharacterMode mode) => mode switch
    {
        CharacterMode.DigitsOnly => DIGITS,
        CharacterMode.LettersOnly => LETTERS_UPPER,
        CharacterMode.Mixed => MIXED,
        CharacterMode.LowercaseMixed => LOWERCASE_MIXED,
        _ => MIXED
    };

    private string GetCharacterPool() => PoolForCharacterMode(SelectedCharacterMode);
    private string GetPasswordCharacterPool() => PoolForCharacterMode(PasswordCharacterMode);

    private string GenerateRandomString(Random rnd, int length, string pool)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = pool[rnd.Next(pool.Length)];
        }
        return new string(chars);
    }

    private Guid? ResolveEffectivePrintTemplateId()
    {
        if (SelectedPrintTemplateOption?.IsProfileDefaultChoice == true)
            return SelectedProfile?.TemplateId ?? (_fallbackSystemTemplateId != Guid.Empty ? _fallbackSystemTemplateId : null);

        if (SelectedPrintTemplateOption?.TemplateId is Guid g)
            return g;

        return _fallbackSystemTemplateId != Guid.Empty ? _fallbackSystemTemplateId : null;
    }

    private async Task GenerateBulkAsync()
    {
        if (SelectedProfile == null)
        {
            ResultMessage = "❌ يرجى اختيار باقة أولاً.";
            HasResult = true;
            return;
        }

        if (Count <= 0 || Count > 10000)
        {
            ResultMessage = "❌ عدد الكروت غير صالح. يجب أن يكون بين 1 و 10,000.";
            HasResult = true;
            return;
        }

        if (!_featureAuthorizationService.CanExecute(FeatureId.VoucherGeneration, Count))
        {
            ResultMessage = $"❌ لا يمكن توليد أكثر من {SecurityConfiguration.MaxFreeVouchersLimit} كرت في النسخة المجانية.";
            HasResult = true;
            return;
        }

        if (SelectedCredentialMode == CredentialMode.UsernameAndPassword && PasswordLength <= 0)
        {
            ResultMessage = "❌ يرجى تحديد طول كلمة السر.";
            HasResult = true;
            return;
        }

        IsGenerating = true;
        HasResult = false;
        ResultMessage = "";
        ProgressCurrent = 0;
        ProgressTotal = Count;
        StatusMessage = "⏳ جاري توليد الكروت...";

        try
        {
            var routerId = _activeRouterContext.CurrentRouterId ?? Guid.Empty;
            var batchId = Guid.NewGuid();
            var newBatch = new Batch
            {
                Id = batchId,
                Name = $"دفعة {DateTime.Now:yyyy-MM-dd HH:mm}",
                ProfileName = SelectedProfile.Name,
                TotalCards = Count,
                RouterId = routerId
            };

            var list = new List<Voucher>();
            var rnd = new Random();
            string pool = GetCharacterPool();
            string passPool = GetPasswordCharacterPool();

            for (int i = 0; i < Count; i++)
            {
                string user = Prefix + GenerateRandomString(rnd, UsernameLength, pool);
                string pass = SelectedCredentialMode switch
                {
                    CredentialMode.UsernameOnly => "",
                    CredentialMode.UsernameEqualsPassword => user,
                    CredentialMode.UsernameAndPassword => PasswordPrefix + GenerateRandomString(rnd, PasswordLength, passPool),
                    _ => ""
                };

                list.Add(new Voucher
                {
                    Username = user,
                    Password = pass,
                    ProfileName = SelectedProfile.Name,
                    BatchId = batchId,
                    Price = Price,
                    CredentialMode = SelectedCredentialMode,
                    AgentId = SelectedAgent?.Id,
                    RouterId = routerId,
                    VoucherSource = VoucherSource.GeneratedByLux,
                    CreatedBy = "System Sweep"
                });

                ProgressCurrent = (int)Math.Max(1, Math.Round((i + 1.0) / Count * (Count * 0.15)));
            }

            ProgressCurrent = (int)(Count * 0.15);
            StatusMessage = "💾 جاري حفظ الكروت في قاعدة البيانات...";
            
            using (var db = await _dbFactory.CreateDbContextAsync())
            {
                var usernames = list.Select(v => v.Username).ToList();
                var existingUsernames = await db.Vouchers
                    .IgnoreQueryFilters()
                    .Where(v => v.RouterId == routerId && usernames.Contains(v.Username))
                    .Select(v => v.Username)
                    .ToListAsync();

                var finalInsertList = new List<Voucher>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int duplicateCount = 0;

                foreach (var v in list)
                {
                    if (existingUsernames.Contains(v.Username) || !seen.Add(v.Username))
                    {
                        duplicateCount++;
                    }
                    else
                    {
                        finalInsertList.Add(v);
                    }
                }

                if (finalInsertList.Count > 0)
                {
                    newBatch.TotalCards = finalInsertList.Count;
                    db.Batches.Add(newBatch);
                    db.Vouchers.AddRange(finalInsertList);
                    await db.SaveChangesAsync();
                }

                ResultMessage = $"✅ تم حفظ {finalInsertList.Count} كرت بنجاح!";
                if (duplicateCount > 0)
                {
                    ResultMessage += $"\n⚠️ تم تجاهل {duplicateCount} كرت مكرر.";
                }
                HasResult = true;
            }

            // Persist last selected print template
            if (SelectedPrintTemplateOption?.TemplateId is Guid tid && !SelectedPrintTemplateOption.IsProfileDefaultChoice)
                _settingsService.Set("Print.LastGenerateTemplateId", tid.ToString());
            else
                _settingsService.Set("Print.LastGenerateTemplateId", string.Empty);
            await _settingsService.SaveAsync();

            // Phase 2: Sync to Router (15% -> 65%)
            bool syncCompletelyFailed = false;
            bool hasSyncFailures = false;

            if (AutoSyncAfterGenerate && list.Count > 0)
            {
                // [FIX-C] Pre-flight check: فحص الاتصال قبل بدء المزامنة
                if (!_activeRouterContext.IsConnected)
                {
                    StatusMessage = "⚠️ الراوتر غير متصل - الكروت محفوظة محلياً وستُزامَن تلقائياً عند عودة الاتصال";
                    ResultMessage += "\n💾 تم حفظ الكروت محلياً. ستُزامَن مع الراوتر تلقائياً عند عودة الاتصال.";
                    syncCompletelyFailed = true;
                    hasSyncFailures = true;
                }
                else
                {
                    StatusMessage = "🔄 جاري المزامنة مع راوتر مايكروتك...";
                    var syncProgress = new Progress<(int success, int failed, int total)>(p =>
                    {
                        int processed = p.success + p.failed;
                        double syncRatio = p.total > 0 ? (double)processed / p.total : 1.0;
                        ProgressCurrent = (int)(Count * 0.15 + syncRatio * (Count * 0.50));
                        StatusMessage = $"🔄 المزامنة: {processed} من {p.total} (نجح {p.success} | فشل {p.failed})...";
                    });

                    try
                    {
                        var syncResult = await _syncService.ProcessBatchAsync(batchId, syncProgress, CancellationToken.None);
                        ResultMessage += $"\n🔄 المزامنة: نجح {syncResult.Success} | فشل {syncResult.Failed}";

                        // [FIX-A] إذا فشلت المزامنة جزئياً أو كلياً → احجب PDF وأعلم المستخدم
                        if (syncResult.Failed > 0 || syncResult.Success < Count)
                        {
                            syncCompletelyFailed = true;
                            hasSyncFailures = true;
                            StatusMessage = "⚠️ لم تكتمل المزامنة مع الراوتر - تم حجب توليد PDF للحفاظ على سلامة الطباعة";
                            ResultMessage += $"\n⚠️ تعذر إنشاء ملف PDF لأن الكروت لم تُضف بالكامل للراوتر بعد.";
                            ResultMessage += "\n💡 يمكنك إعادة محاولة المزامنة ثم الطباعة من شاشة إدارة الكروت عند استقرار الاتصال.";
                        }
                    }
                    catch (Exception syncEx)
                    {
                        _logger.LogError(syncEx, "Error during batch sync to MikroTik");
                        syncCompletelyFailed = true;
                        hasSyncFailures = true;
                        StatusMessage = "⚠️ انقطع الاتصال بالراوتر أثناء المزامنة - تم حجب توليد PDF للحفاظ على سلامة الطباعة";
                        ResultMessage += $"\n⚠️ تعذر استكمال المزامنة مع الراوتر بسبب انقطاع الاتصال: {syncEx.Message}";
                        ResultMessage += "\n💡 تم حفظ الكروت محلياً، وستُزامَن تلقائياً عند عودة الاتصال.";
                    }
                }
            }

            // Phase 3: Print PDF Generation (65% -> 100%)
            // [FIX-A] PDF تُولَّد فقط إذا تم رفع جميع الكروت للراوتر بنجاح بدون أي فشل
            if (PrintAfterGenerate && list.Count > 0 && !syncCompletelyFailed)
            {
                StatusMessage = "🖨️ جاري تحضير ملف الطباعة ورسم الصفحات...";
                int baseProgress = ProgressCurrent;
                double printWeight = Count - baseProgress;

                var printProgress = new Progress<(int currentPage, int totalPages, string statusText)>(p =>
                {
                    double pageRatio = p.totalPages > 0 ? (double)p.currentPage / p.totalPages : 1.0;
                    ProgressCurrent = (int)(baseProgress + pageRatio * printWeight);
                    StatusMessage = $"🖨️ {p.statusText}";
                });

                await AutoPrintLastBatchAsync(batchId, printProgress, CancellationToken.None);
            }

            ProgressCurrent = Count;
            if (hasSyncFailures || syncCompletelyFailed)
            {
                StatusMessage = "⚠️ اكتمل توليد الكروت محلياً مع وجود كروت معلقة للمزامنة";
                HasResult = true;
                // لا تُغلق النافذة تلقائياً لتتيح للمستخدم قراءة التنبيه والنتائج
            }
            else
            {
                StatusMessage = "🎉 اكتملت العملية بنجاح!";
                await Task.Delay(2000);
                RequestClose?.Invoke(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating batch");
            ResultMessage = $"❌ حدث خطأ: {ex.Message}";
            HasResult = true;
            StatusMessage = "❌ فشلت العملية";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task AutoPrintLastBatchAsync(
        Guid batchId,
        IProgress<(int currentPage, int totalPages, string statusText)>? printProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var vouchers = await db.Vouchers
                .IgnoreQueryFilters()
                .Include(v => v.Agent)
                .Where(v => v.BatchId == batchId)
                .Select(v => new VoucherDto
                {
                    Id = v.Id,
                    Username = v.Username,
                    Password = v.Password,
                    Profile = v.ProfileName,
                    Price = v.Price,
                    Status = v.Status,
                    AgentName = v.Agent != null ? v.Agent.Name : "-"
                })
                .ToListAsync(cancellationToken);

            if (vouchers.Count == 0) return;

            var settings = new PrintSettingsDto
            {
                CompressOutput = true,
                ImageQuality = 45,
                MaxImageSidePx = 400
            };
            var tid = ResolveEffectivePrintTemplateId();
            if (tid.HasValue)
                settings.CustomTemplateId = tid.Value;

            var pdfResult = await _printService.GeneratePdfAsync(vouchers, settings, printProgress, cancellationToken);
            if (pdfResult.IsSuccess && pdfResult.Value != null && pdfResult.Value.Length > 0)
            {
                string targetPath = !string.IsNullOrWhiteSpace(CustomSavePath)
                    ? CustomSavePath
                    : System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"luxcard_batch_{DateTime.Now:HHmmss}.pdf");

                await System.IO.File.WriteAllBytesAsync(targetPath, pdfResult.Value, cancellationToken);

                var fi = new System.IO.FileInfo(targetPath);
                if (fi.Exists && fi.Length > 0)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(targetPath) { UseShellExecute = true });

                    ResultMessage += $"\n🖨️ تم توليد وحفظ ملف الطباعة بنجاح: {System.IO.Path.GetFileName(targetPath)}";
                }
                else
                {
                    _logger.LogWarning("PDF file was written but size is zero: {TargetPath}", targetPath);
                    ResultMessage += "\n⚠️ تعذر فتح ملف الطباعة لأن الحجم غير مكتمل.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشلت الطباعة التلقائية");
            ResultMessage += "\n⚠️ فشلت الطباعة التلقائية";
        }
    }
}
