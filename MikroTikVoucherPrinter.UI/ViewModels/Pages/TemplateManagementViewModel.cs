using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

public partial class TemplateManagementViewModel : BaseViewModel
{
    private readonly IGenericRepository<TemplateConfig> _templateRepo;
    private readonly IProfileService _profileService;
    private readonly IPrintService _printService;

    // طھطھط¨ط¹ ط§ظ„ظ‚ظˆط§ظ„ط¨ ط§ظ„ط¬ط¯ظٹط¯ط© ط§ظ„طھظٹ ظ„ظ… طھظڈط­ظپط¸ ط¨ط¹ط¯ ظپظٹ ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ
    private readonly HashSet<Guid> _pendingAdd = new();

    public ObservableCollection<TemplateConfig> Templates { get; } = new();
    public ObservableCollection<string> AvailableProfiles { get; } = new();
    
    private ObservableCollection<int> _previewCards = new();
    public ObservableCollection<int> PreviewCards 
    {
        get => _previewCards;
        set => SetProperty(ref _previewCards, value);
    }

    private bool _isSingleCardPreview = true;
    public bool IsSingleCardPreview
    {
        get => _isSingleCardPreview;
        set
        {
            if (SetProperty(ref _isSingleCardPreview, value))
            {
                OnPropertyChanged(nameof(IsA4Preview));
            }
        }
    }
    public bool IsA4Preview
    {
        get => !IsSingleCardPreview;
        set => IsSingleCardPreview = !value;
    }

    private TemplateConfig? _selectedTemplate;
    public TemplateConfig? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            SetProperty(ref _selectedTemplate, value);
            NotifyCommands();
            UpdatePreviewCards();
        }
    }

    private string _resultMessage = "";
    public string ResultMessage
    {
        get => _resultMessage;
        set => SetProperty(ref _resultMessage, value);
    }

    private bool _hasResult;
    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    private bool _compressOutput = false;
    public bool CompressOutput
    {
        get => _compressOutput;
        set => SetProperty(ref _compressOutput, value);
    }

    public TemplateManagementViewModel(
        IGenericRepository<TemplateConfig> templateRepo,
        IProfileService profileService,
        IPrintService printService,
        ILogger<TemplateManagementViewModel> logger) : base(logger)
    {
        _templateRepo = templateRepo;
        _profileService = profileService;
        _printService = printService;
        Title = "ط¥ط¯ط§ط±ط© ظ‚ظˆط§ظ„ط¨ ط§ظ„ط·ط¨ط§ط¹ط© ط§ظ„ظ…ط®طµطµط©";

        LoadCommand = new AsyncRelayCommand(LoadTemplatesAsync);
        AddNewCommand = new RelayCommand(AddNewTemplate);
        SaveCommand = new AsyncRelayCommand(SaveTemplateAsync, () => SelectedTemplate != null);
        DeleteCommand = new AsyncRelayCommand(DeleteTemplateAsync, () => SelectedTemplate != null);
        BrowseBackgroundCommand = new RelayCommand(BrowseBackground, () => SelectedTemplate != null);
        BrowseLogoCommand = new RelayCommand(BrowseLogo, () => SelectedTemplate != null);
        PreviewTemplateCommand = new AsyncRelayCommand(PreviewTemplateAsync, () => SelectedTemplate != null);
    }

    public override async Task InitializeAsync(object? parameter = null)
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
                foreach (var t in data) Templates.Add(t);
                SelectedTemplate = Templates.FirstOrDefault();
            });
        }, "ط¬ط§ط±ظٹ طھط­ظ…ظٹظ„ ط§ظ„ظ‚ظˆط§ظ„ط¨...");
    }

    private async Task LoadProfilesAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var profiles = await _profileService.GetAllProfilesAsync(MikroTikVoucherPrinter.Domain.Enums.PackageSourceType.UserManager, token);
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
        }, "ط¬ط§ط±ظٹ ط¬ظ„ط¨ ط§ظ„ط¨ط§ظ‚ط§طھ ظ…ظ† ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظٹظƒ...");
    }

    private void AddNewTemplate()
    {
        var tpl = new TemplateConfig
        {
            Name = "ظ‚ط§ظ„ط¨ ط¬ط¯ظٹط¯",
            IsDefault = false,
            ShowUsername = true,
            ShowPassword = false,
            ShowPrice = false,
            ShowQr = false,
            Columns = 4,
            Rows = 22,
            MarginX = 1.0f,
            MarginY = 1.0f,
            UsernameX = 17.7f,
            UsernameY = 3.8f,
            PasswordX = 5.0f,
            PasswordY = 12.0f,
            PriceX = 5.0f,
            PriceY = 20.0f,
            QrX = 40.0f,
            QrY = 5.0f,
            ValidityX = 5.0f,
            ValidityY = 28.0f,
            TimeX = 5.0f,
            TimeY = 36.0f,
            SerialNumberX = 5.0f,
            SerialNumberY = 44.0f,
            PrintDateX = 40.0f,
            PrintDateY = 44.0f,
            BarcodeX = 30.0f,
            BarcodeY = 20.0f,
            FontSize = 5.0f,
            FontFamily = "Arial",
            FontColorHex = "#000000",
            FrameColorHex = "#000000",
            FrameSize = 0
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

            // ط¥ط°ط§ ظƒط§ظ† ظ‡ط°ط§ ط§ظ„ط§ظپطھط±ط§ط¶ظٹطŒ ط§ظ„ط؛ظٹ ط§ظ„ط¨ظ‚ظٹط©
            if (SelectedTemplate.IsDefault)
            {
                var others = Templates.Where(x => x.Id != SelectedTemplate.Id && x.IsDefault).ToList();
                foreach (var o in others)
                {
                    o.IsDefault = false;
                    await _templateRepo.UpdateAsync(o, token);
                }
            }

        }, "ط¬ط§ط±ظٹ ط§ظ„ط­ظپط¸...");

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (HasError)
            {
                System.Windows.MessageBox.Show($"ط­ط¯ط« ط®ط·ط£ ط£ط«ظ†ط§ط، ط§ظ„ط­ظپط¸:\n{ErrorMessage}", "ط®ط·ط£ ظپظٹ ط§ظ„ط­ظپط¸", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            else
            {
                ResultMessage = "âœ… طھظ… ط­ظپط¸ ط§ظ„ظ‚ط§ظ„ط¨ ط¨ظ†ط¬ط§ط­";
                HasResult = true;
                System.Windows.MessageBox.Show("طھظ… ط§ظ„ط­ظپط¸ ط¨ظ†ط¬ط§ط­!", "طھط£ظƒظٹط¯", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
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
        }, "ط¬ط§ط±ظٹ ط§ظ„ط­ط°ظپ...");
    }

    private void BrowseBackground()
    {
        if (SelectedTemplate == null) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "ط§ط®طھط± طµظˆط±ط© ط§ظ„ط®ظ„ظپظٹط© ظ„ظ„ظƒط±طھ",
            Filter = "ظ…ظ„ظپط§طھ ط§ظ„طµظˆط±|*.png;*.jpg;*.jpeg|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedTemplate.BackgroundImagePath = dialog.FileName;
            RefreshSelectedTemplate();
        }
    }

    private void BrowseLogo()
    {
        if (SelectedTemplate == null) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "ط§ط®طھط± طµظˆط±ط© ط§ظ„ط´ط¹ط§ط± (Logo)",
            Filter = "ظ…ظ„ظپط§طھ ط§ظ„طµظˆط±|*.png;*.jpg;*.jpeg|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedTemplate.LogoImagePath = dialog.FileName;
            RefreshSelectedTemplate();
        }
    }

    public void RefreshSelectedTemplate()
    {
        // TemplateConfig ظ„ط§ ظٹط·ط¨ظ‚ INotifyPropertyChangedطŒ ظ„ط°ظ„ظƒ ظ†ط¹ظٹط¯ ط±ط¨ط· ط§ظ„ظ…ط±ط¬ط¹ ظ„طھط­ط¯ظٹط« ط§ظ„ظˆط§ط¬ظ‡ط© ظپظˆط±ط§ظ‹
        var current = SelectedTemplate;
        if (current == null) return;
        _selectedTemplate = null;
        OnPropertyChanged(nameof(SelectedTemplate));
        _selectedTemplate = current;
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

    private void NotifyCommands()
    {
        (SaveCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (DeleteCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (BrowseBackgroundCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (BrowseLogoCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (PreviewTemplateCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    // â”€â”€â”€ Preview: طھظˆظ„ظٹط¯ PDF ظˆظ‡ظ…ظٹ ظ„ط§ط®طھط¨ط§ط± ط§ظ„ظ‚ط§ظ„ط¨ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private async Task PreviewTemplateAsync()
    {
        if (SelectedTemplate == null) return;

        // â”€â”€â”€ ط§ط®طھظٹط§ط± ظ…ظƒط§ظ† ط§ظ„ط­ظپط¸ (SaveFileDialog) â”€â”€â”€
        string? savePath = null;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "ط§ط®طھط± ظ…ظƒط§ظ† ط­ظپط¸ ظ…ظ„ظپ ط§ظ„ظ…ط¹ط§ظٹظ†ط©",
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = $"LuxCard_Preview_{SelectedTemplate.Name}_{DateTime.Now:HHmmss}.pdf"
            };
            if (dlg.ShowDialog() == true)
                savePath = dlg.FileName;
        });

        if (savePath == null) return; // ط£ظ„ط؛ظ‰ ط§ظ„ظ…ط³طھط®ط¯ظ…

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
                using var ms = new System.IO.MemoryStream();
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

            // ط­ظپط¸ ط§ظ„ظ…ظ„ظپ ظپظٹ ط§ظ„ظ…ط³ط§ط± ط§ظ„ظ…ط®طھط§ط±
            await System.IO.File.WriteAllBytesAsync(savePath, result, token);

            // ظپطھط­ ط§ظ„ظ…ظ„ظپ ط¨ط§ظ„ط¹ط§ط±ط¶ ط§ظ„ط§ظپطھط±ط§ط¶ظٹ
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = savePath,
                    UseShellExecute = true
                });
            });

        }, "ط¬ط§ط±ظٹ طھظˆظ„ظٹط¯ ظ…ط¹ط§ظٹظ†ط© PDF...");
    }

    public IAsyncRelayCommand LoadCommand { get; }
    public IRelayCommand AddNewCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IRelayCommand BrowseBackgroundCommand { get; }
    public IRelayCommand BrowseLogoCommand { get; }
    public IAsyncRelayCommand PreviewTemplateCommand { get; }
}
