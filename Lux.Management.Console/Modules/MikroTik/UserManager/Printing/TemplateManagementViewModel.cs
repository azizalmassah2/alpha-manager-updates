using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Management.Console.Core;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Printing.ViewModels
{
    public partial class TemplateManagementViewModel : Lux.Management.Console.ViewModels.ViewModelBase
    {
        private readonly IGenericRepository<TemplateConfig> _templateRepo;
        private readonly IProfileService _profileService;
        private readonly IPrintService _printService;
        private readonly IActiveRouterContext _activeRouterContext;
        private readonly ILogger<TemplateManagementViewModel> _logger;

        // تتتبع القوالب الجديدة التي لم تُحفظ بعد في قاعدة البيانات
        private readonly HashSet<Guid> _pendingAdd = new();

        public ObservableCollection<TemplateConfig> Templates { get; } = new();
        public ObservableCollection<string> AvailableProfiles { get; } = new();

        [ObservableProperty]
        private ObservableCollection<int> _previewCards = new();

        [ObservableProperty]
        private bool _isSingleCardPreview = true;

        public bool IsA4Preview
        {
            get => !IsSingleCardPreview;
            set => IsSingleCardPreview = !value;
        }

        [ObservableProperty]
        private TemplateConfig? _selectedTemplate;

        [ObservableProperty]
        private ImageSource? _cardPreviewImage;

        partial void OnSelectedTemplateChanged(TemplateConfig? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            BrowseBackgroundCommand.NotifyCanExecuteChanged();
            BrowseLogoCommand.NotifyCanExecuteChanged();
            PreviewTemplateCommand.NotifyCanExecuteChanged();

            if (value != null)
            {
                value.PropertyChanged -= SelectedTemplate_PropertyChanged;
                value.PropertyChanged += SelectedTemplate_PropertyChanged;
            }

            UpdatePreviewCards();
            UpdateCardPreviewImage();
        }

        private void SelectedTemplate_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateCardPreviewImage();
        }

        public void UpdateCardPreviewImage()
        {
            if (SelectedTemplate == null) return;
            try
            {
                var dummyVoucher = new VoucherDto
                {
                    Id = Guid.NewGuid(),
                    Username = "123456789",
                    Password = "123456789",
                    Profile = SelectedTemplate.LinkedProfileName ?? "300MB",
                    Price = 500,
                    CredentialMode = CredentialMode.UsernameAndPassword,
                    Status = VoucherStatus.Unused
                };
                byte[] bytes = MikroTikVoucherPrinter.Infrastructure.Printing.VoucherCardGraphicRenderer.RenderCardToPngBytes(SelectedTemplate, dummyVoucher, dpi: 300);
                using var ms = new MemoryStream(bytes);
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                CardPreviewImage = bitmap;
            }
            catch { /* Ignore */ }
        }

        [ObservableProperty]
        private string _resultMessage = "";

        [ObservableProperty]
        private bool _hasResult;

        [ObservableProperty]
        private bool _compressOutput = true; // Enabled by default to reduce file size

        public TemplateManagementViewModel(
            IGenericRepository<TemplateConfig> templateRepo,
            IProfileService profileService,
            IPrintService printService,
            IActiveRouterContext activeRouterContext,
            ILogger<TemplateManagementViewModel> logger,
            IPermissionService permissionService,
            IEventBus eventBus) : base(permissionService, eventBus)
        {
            _templateRepo = templateRepo;
            _profileService = profileService;
            _printService = printService;
            _activeRouterContext = activeRouterContext;
            _logger = logger;
            Title = "إدارة قوالب الطباعة المخصصة";

            LoadCommand = new AsyncRelayCommand(LoadTemplatesAsync);
            AddNewCommand = new AsyncRelayCommand(AddNewTemplateAsync);
            SaveCommand = new AsyncRelayCommand(SaveTemplateAsync, () => SelectedTemplate != null);
            DeleteCommand = new AsyncRelayCommand(DeleteTemplateAsync, () => SelectedTemplate != null);
            BrowseBackgroundCommand = new RelayCommand(BrowseBackground, () => SelectedTemplate != null);
            BrowseLogoCommand = new RelayCommand(BrowseLogo, () => SelectedTemplate != null);
            PreviewTemplateCommand = new AsyncRelayCommand(PreviewTemplateAsync, () => SelectedTemplate != null);
        }

        public async Task InitializeAsync()
        {
            await LoadTemplatesAsync();
            await LoadProfilesAsync();
        }

        private async Task LoadTemplatesAsync()
        {
            await ExecuteBusyAsync(async (token) =>
            {
                var data = await _templateRepo.ListAsync(token);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Templates.Clear();
                    foreach (var t in data) 
                    {
                        // إخفاء القوالب النظامية من مصمم القوالب المخصصة
                        if (!t.IsSystemTemplate)
                        {
                            Templates.Add(t);
                        }
                    }
                    SelectedTemplate = Templates.FirstOrDefault();
                });
            }, "جاري تحميل القوالب...");
        }

        private async Task LoadProfilesAsync()
        {
            await ExecuteBusyAsync(async (token) =>
            {
                var profiles = await _profileService.GetAllProfilesAsync(PackageSourceType.UserManager, token);
                var names = profiles
                    .Select(p => p.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableProfiles.Clear();
                    foreach (var name in names)
                    {
                        AvailableProfiles.Add(name);
                    }
                });
            }, "جاري جلب الباقات من المايكروتك...");
        }

        private async Task AddNewTemplateAsync()
        {
            // 1. حفظ القالب النشط حالياً تلقائياً إذا كان موجوداً
            if (SelectedTemplate != null)
            {
                await SaveTemplateAsync();
            }

            // 2. إظهار حوار إدخال اسم القالب الجديد والباقة
            string name = string.Empty;
            string? profile = null;
            bool confirmed = false;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var activeWindow = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(x => x.IsActive)
                                   ?? System.Windows.Application.Current.MainWindow;

                var dlg = new Views.Dialogs.NewTemplateDialog(activeWindow!, AvailableProfiles);
                if (dlg.ShowDialog() == true)
                {
                    name = dlg.TemplateName;
                    profile = dlg.SelectedProfile;
                    confirmed = true;
                }
            });

            if (!confirmed) return;

            // 3. توريث خصائص آخر قالب تم تعديله (أو القوالب الافتراضية للقطة الشاشة)
            var source = SelectedTemplate;
            var tpl = new TemplateConfig
            {
                Id = Guid.NewGuid(),
                Name = name,
                LinkedProfileName = profile,
                IsDefault = false,
                IsSystemTemplate = false,
                RouterId = _activeRouterContext.CurrentRouterId ?? Guid.Empty,

                // وراثة الأبعاد أو تعيين الخواص الافتراضية
                Columns = source?.Columns ?? 4,
                Rows = source?.Rows ?? 21,
                MarginX = source?.MarginX ?? 1.0f,
                MarginY = source?.MarginY ?? 1.0f,
                UsernameX = source?.UsernameX ?? 20.0f,
                UsernameY = source?.UsernameY ?? 4.3f,
                PasswordX = source?.PasswordX ?? 5.0f,
                PasswordY = source?.PasswordY ?? 12.0f,
                PriceX = source?.PriceX ?? 5.0f,
                PriceY = source?.PriceY ?? 20.0f,
                QrX = source?.QrX ?? 40.0f,
                QrY = source?.QrY ?? 5.0f,
                ValidityX = source?.ValidityX ?? 5.0f,
                ValidityY = source?.ValidityY ?? 28.0f,
                TimeX = source?.TimeX ?? 5.0f,
                TimeY = source?.TimeY ?? 36.0f,
                SerialNumberX = source?.SerialNumberX ?? 5.0f,
                SerialNumberY = source?.SerialNumberY ?? 44.0f,
                PrintDateX = source?.PrintDateX ?? 40.0f,
                PrintDateY = source?.PrintDateY ?? 44.0f,
                BarcodeX = source?.BarcodeX ?? 30.0f,
                BarcodeY = source?.BarcodeY ?? 20.0f,
                FontSize = source?.FontSize ?? 5.0f,
                FontFamily = source?.FontFamily ?? "Arial",
                FontColorHex = source?.FontColorHex ?? "#000000",
                FrameColorHex = source?.FrameColorHex ?? "#000000",
                FrameSize = source?.FrameSize ?? 0,
                BackgroundImagePath = source?.BackgroundImagePath,
                LogoImagePath = source?.LogoImagePath,
                ShowUsername = source?.ShowUsername ?? true,
                ShowPassword = source?.ShowPassword ?? false,
                ShowPrice = source?.ShowPrice ?? false,
                ShowQr = source?.ShowQr ?? false,
                ShowValidity = source?.ShowValidity ?? false,
                ShowTime = source?.ShowTime ?? false,
                ShowSerialNumber = source?.ShowSerialNumber ?? false,
                ShowPrintDate = source?.ShowPrintDate ?? false,
                ShowBarcode = source?.ShowBarcode ?? false
            };

            Templates.Add(tpl);
            _pendingAdd.Add(tpl.Id);   // mark as new (not yet in DB)
            SelectedTemplate = tpl;
        }

        private async Task SaveTemplateAsync()
        {
            if (SelectedTemplate == null) return;

            await ExecuteBusyAsync(async (token) =>
            {
                bool isNew = _pendingAdd.Contains(SelectedTemplate.Id);

                if (isNew)
                {
                    await _templateRepo.AddAsync(SelectedTemplate, token);
                    _pendingAdd.Remove(SelectedTemplate.Id); // now it's in DB
                }
                else
                {
                    await _templateRepo.UpdateAsync(SelectedTemplate, token);
                }

                // إذا كان هذا الافتراضي، الغي البقية
                if (SelectedTemplate.IsDefault)
                {
                    var others = Templates.Where(x => x.Id != SelectedTemplate.Id && x.IsDefault).ToList();
                    foreach (var o in others)
                    {
                        o.IsDefault = false;
                        await _templateRepo.UpdateAsync(o, token);
                    }
                }

            }, "جاري الحفظ...");

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (IsBusy) return; // if it failed but didn't finish properly
                
                ResultMessage = "✅ تم حفظ القالب بنجاح";
                HasResult = true;
            });
        }

        private async Task DeleteTemplateAsync()
        {
            if (SelectedTemplate == null) return;

            await ExecuteBusyAsync(async (token) =>
            {
                if (SelectedTemplate.Id != Guid.Empty && !_pendingAdd.Contains(SelectedTemplate.Id))
                    await _templateRepo.SoftDeleteAsync(SelectedTemplate, token);

                _pendingAdd.Remove(SelectedTemplate.Id);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Templates.Remove(SelectedTemplate);
                    SelectedTemplate = Templates.FirstOrDefault();
                });
            }, "جاري الحذف...");
        }

        private void BrowseBackground()
        {
            if (SelectedTemplate == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "اختر صورة الخلفية للكرت",
                Filter = "ملفات الصور|*.png;*.jpg;*.jpeg|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                // INotifyPropertyChanged on TemplateConfig fires automatically,
                // updating the Image binding without needing to refresh the DataContext.
                SelectedTemplate.BackgroundImagePath = dialog.FileName;
                System.Windows.MessageBox.Show($"[DEBUG] تم تعيين مسار خلفية الكرت بنجاح إلى:\n{dialog.FileName}", "نجاح تعيين الخلفية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void BrowseLogo()
        {
            if (SelectedTemplate == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "اختر صورة الشعار (Logo)",
                Filter = "ملفات الصور|*.png;*.jpg;*.jpeg|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                // INotifyPropertyChanged fires automatically — no Refresh needed.
                SelectedTemplate.LogoImagePath = dialog.FileName;
                System.Windows.MessageBox.Show($"[DEBUG] تم تعيين مسار شعار الشبكة بنجاح إلى:\n{dialog.FileName}", "نجاح تعيين الشعار", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        public void RefreshSelectedTemplate()
        {
            if (SelectedTemplate == null) return;
            // Raise PropertyChanged for SelectedTemplate WITHOUT nulling it first.
            // Nulling causes all Canvas Thumb bindings to be recreated from scratch,
            // which resets the visual positions even if the stored X/Y values are correct.
            OnPropertyChanged(nameof(SelectedTemplate));
            UpdatePreviewCards();
        }

        public void UpdatePreviewCards()
        {
            if (SelectedTemplate == null) return;

            var count = SelectedTemplate.Columns * SelectedTemplate.Rows;
            if (count > 400) count = 400; // safety limit

            var list = new ObservableCollection<int>();
            for (int i = 0; i < count; i++)
            {
                list.Add(i);
            }
            PreviewCards = list;
        }

        // ─── Preview: توليد PDF وهمي لاختبار القالب ─────────────────────────
        private async Task PreviewTemplateAsync()
        {
            if (SelectedTemplate == null) return;

            string? savePath = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "اختر مكان حفظ ملف المعاينة",
                    Filter = "PDF files (*.pdf)|*.pdf",
                    DefaultExt = "pdf",
                    FileName = $"LuxCard_Preview_{SelectedTemplate.Name}_{DateTime.Now:HHmmss}.pdf"
                };
                if (dlg.ShowDialog() == true)
                    savePath = dlg.FileName;
            });

            if (savePath == null) return; // ألغى المستخدم

            await ExecuteBusyAsync(async (token) =>
            {
                var t = SelectedTemplate;
                int count = Math.Max(1, t.Columns * t.Rows);
                bool compress = CompressOutput;

                var fakeVouchers = Enumerable.Range(1, count).Select(i => new VoucherDto
                {
                    Id = Guid.NewGuid(),
                    Username = "123456789",
                    Password = "123456789",
                    Profile = t.LinkedProfileName ?? "200MB",
                    Price = 500,
                    CredentialMode = CredentialMode.UsernameAndPassword,
                    Status = VoucherStatus.Unused
                }).ToList();

                var settings = new PrintSettingsDto
                {
                    PaperType = PaperType.A4,
                    FontSize = (int)Math.Max(6, t.FontSize),
                    QrBaseUrl = "http://hotspot.local/login",
                    ShowQrCode = t.ShowQr,
                    CompressOutput = compress,
                    ImageQuality = 40,
                    MaxImageSidePx = 400,
                };

                var result = await Task.Run(() =>
                {
                    using var ms = new MemoryStream();
                    using var writer = new iText.Kernel.Pdf.PdfWriter(ms);
                    using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
                    var pageSize = iText.Kernel.Geom.PageSize.A4;
                    var document = new iText.Layout.Document(pdf, pageSize);
                    document.SetMargins(2, 2, 2, 2);

                    iText.Kernel.Font.PdfFont arabicFont;
                    try { arabicFont = iText.Kernel.Font.PdfFontFactory.CreateFont("c:\\windows\\fonts\\tahoma.ttf", iText.IO.Font.PdfEncodings.IDENTITY_H); }
                    catch { arabicFont = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA); }

                    var template = new MikroTikVoucherPrinter.Infrastructure.Templates.CustomGridTemplate(t);
                    template.LayoutDocument(document, fakeVouchers, settings, arabicFont);
                    document.Close();
                    return ms.ToArray();
                }, token);

                await File.WriteAllBytesAsync(savePath, result, token);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = savePath,
                        UseShellExecute = true
                    });
                });

            }, "جاري توليد معاينة PDF...");
        }

        public IAsyncRelayCommand LoadCommand { get; }
        public IAsyncRelayCommand AddNewCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }
        public IRelayCommand BrowseBackgroundCommand { get; }
        public IRelayCommand BrowseLogoCommand { get; }
        public IAsyncRelayCommand PreviewTemplateCommand { get; }
    }
}
