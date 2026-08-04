using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using System.Text.Json;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Microsoft.Web.WebView2.Core;

using System.Windows.Media.Imaging;
using Lux.Management.Console.Modules.MikroTik.Hotspot.Models;

namespace Lux.Management.Console.Modules.MikroTik.Hotspot.ViewModels
{
    public partial class HotspotLoginViewModel : ViewModelBase, IActivatable
    {
        private readonly IHotspotService _hotspotService;
        private readonly IActiveRouterContext _activeRouterContext;
        private readonly ISecureStorageService _secureStorageService;
        private readonly IDispatcherService _dispatcherService;
        private readonly ISettingsService _settingsService;

        [ObservableProperty] private string _siteName = string.Empty;
        [ObservableProperty] private string _welcomeMessage = string.Empty;
        [ObservableProperty] private bool _welcomeMessageV;
        [ObservableProperty] private bool _erbV;
        [ObservableProperty] private string _textSlider1 = string.Empty;
        [ObservableProperty] private string _imageCount = "1";
        [ObservableProperty] private int _imageCountValue = 1;
        [ObservableProperty] private bool _imageV;
        [ObservableProperty] private string _offers = string.Empty;
        [ObservableProperty] private string _estr = string.Empty;
        [ObservableProperty] private string _moba = string.Empty;
        [ObservableProperty] private string _supportPhone = string.Empty;
        [ObservableProperty] private string _developerName = string.Empty;
        [ObservableProperty] private string _developerPhone = string.Empty;
        [ObservableProperty] private string _activeTheme = "sakura";

        [ObservableProperty] private string _destinationPath = "hotspot";
        [ObservableProperty] private bool _isConnected;
        [ObservableProperty] private bool _hasValidRouterConfig;
        [ObservableProperty] private string _routerName = "—";
        [ObservableProperty] private string _routerHost = "—";

        [ObservableProperty] private bool _isUploading;
        [ObservableProperty] private double _uploadProgress;
        [ObservableProperty] private string _uploadStatus = string.Empty;

        // DTO Collections
        public ObservableCollection<SpeedOptionDto> SpeedOptions { get; } = new();
        public ObservableCollection<HotspotPackageDto> Packages { get; } = new();
        public ObservableCollection<string> SalesPoints { get; } = new();
        public ObservableCollection<HotspotTheme> AvailableThemes { get; } = new();
        public ObservableCollection<AdImageItemDto> AdImageItems { get; } = new();

        // For adding new items
        [ObservableProperty] private string _newSpeedValue = string.Empty;
        [ObservableProperty] private string _newSpeedLabel = string.Empty;

        [ObservableProperty] private string _newPackageValidity = string.Empty;
        [ObservableProperty] private string _newPackageTime = string.Empty;
        [ObservableProperty] private string _newPackageSize = string.Empty;
        [ObservableProperty] private string _newPackagePrice = string.Empty;

        [ObservableProperty] private string _newSalesPoint = string.Empty;

        private CancellationTokenSource? _uploadCts;

        public HotspotLoginViewModel(
            IPermissionService permissionService,
            IEventBus eventBus,
            IHotspotService hotspotService,
            IActiveRouterContext activeRouterContext,
            ISecureStorageService secureStorageService,
            IDispatcherService dispatcherService,
            ISettingsService settingsService)
            : base(permissionService, eventBus)
        {
            _hotspotService = hotspotService;
            _activeRouterContext = activeRouterContext;
            _secureStorageService = secureStorageService;
            _dispatcherService = dispatcherService;
            _settingsService = settingsService;

            // Load saved destination folder persistently
            DestinationPath = _settingsService.Get("Hotspot_DestinationPath", "hotspot");

            InitializeThemes();

            _activeRouterContext.ActiveRouterChanged += async (s, e) =>
            {
                UpdateRouterInfo();
                await LoadConfigAndSyncAsync(allowInteractiveFolderPicker: false);
            };
            UpdateRouterInfo();
        }

        partial void OnDestinationPathChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _settingsService.Set("Hotspot_DestinationPath", value.Trim());
            }
        }

        public async Task ActivateAsync()
        {
            await LoadConfigAndSyncAsync(allowInteractiveFolderPicker: false);
            UpdateRouterInfo();
        }

        private void UpdateRouterInfo()
        {
            _dispatcherService.Invoke(() =>
            {
                IsConnected = _activeRouterContext.IsConnected;
                var router = _activeRouterContext.CurrentRouter;
                if (router != null)
                {
                    RouterName = router.DisplayName;
                    RouterHost = router.Host;
                }
                else
                {
                    RouterName = "لا يوجد راوتر نشط";
                    RouterHost = "—";
                }
            });
        }

        partial void OnImageCountValueChanged(int value)
        {
            if (value < 1) value = 1;
            if (value > 5) value = 5;
            ImageCount = value.ToString();
            UpdateAdImageItemsList();
        }

        partial void OnImageCountChanged(string value)
        {
            if (int.TryParse(value, out int parsed))
            {
                if (parsed < 1) parsed = 1;
                if (parsed > 5) parsed = 5;
                if (ImageCountValue != parsed)
                {
                    ImageCountValue = parsed;
                }
            }
            else
            {
                ImageCountValue = 1;
            }
            UpdateAdImageItemsList();
        }

        private void UpdateAdImageItemsList()
        {
            int targetCount = ImageCountValue;
            if (targetCount < 1) targetCount = 1;
            if (targetCount > 5) targetCount = 5;

            while (AdImageItems.Count > targetCount)
            {
                AdImageItems.RemoveAt(AdImageItems.Count - 1);
            }

            string[] ordinals = { "الأولى", "الثانية", "الثالثة", "الرابعة", "الخامسة" };

            for (int i = 1; i <= targetCount; i++)
            {
                if (AdImageItems.Count < i)
                {
                    var item = new AdImageItemDto
                    {
                        Index = i,
                        DisplayName = $"الصورة {ordinals[i - 1]} ({i}.jpg)"
                    };

                    var savedPath = _hotspotService.GetAdImagePath(i);
                    if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
                    {
                        item.LocalFilePath = savedPath;
                        item.HasImage = true;
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(savedPath);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            item.PreviewImage = bitmap;
                        }
                        catch { }
                    }

                    AdImageItems.Add(item);
                }
            }
        }

        [RelayCommand]
        private void SelectAdImage(AdImageItemDto? item)
        {
            if (item == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"اختيار الصورة الإعلانية رقم {item.Index}",
                Filter = "ملفات الصور (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|جميع الملفات (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var selectedFile = dialog.FileName;
                    _hotspotService.SaveAdImage(item.Index, selectedFile);

                    var savedPath = _hotspotService.GetAdImagePath(item.Index);
                    item.LocalFilePath = selectedFile;
                    item.HasImage = true;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(selectedFile);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    item.PreviewImage = bitmap;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ أثناء تحميل الصورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void RemoveAdImage(AdImageItemDto? item)
        {
            if (item == null) return;
            _hotspotService.DeleteAdImage(item.Index);
            item.LocalFilePath = null;
            item.PreviewImage = null;
            item.HasImage = false;
        }

        private void LoadFromConfig()
        {
            var config = _hotspotService.LoadConfig();

            SiteName = config.SiteName;
            WelcomeMessage = config.WelcomeMessage;
            WelcomeMessageV = config.WelcomeMessageV;
            ErbV = config.ErbV;
            TextSlider1 = config.TextSlider1;
            ImageCount = config.ImageCount;
            if (int.TryParse(config.ImageCount, out int count) && count >= 1 && count <= 5)
            {
                ImageCountValue = count;
            }
            else
            {
                ImageCountValue = 1;
            }
            ImageV = config.ImageV;
            Offers = config.Offers;
            Estr = config.Estr;
            Moba = config.Moba;
            SupportPhone = config.SupportPhone;
            DeveloperName = "م/ عزيز المساح";
            DeveloperPhone = "771122633";
            ActiveTheme = config.ActiveTheme;

            SpeedOptions.Clear();
            foreach (var opt in config.SpeedOptions)
                SpeedOptions.Add(opt);

            Packages.Clear();
            foreach (var pkg in config.Packages)
                Packages.Add(pkg);

            SalesPoints.Clear();
            foreach (var pt in config.SalesPoints)
                SalesPoints.Add(pt);

            UpdateAdImageItemsList();
        }

        private void SaveToConfig()
        {
            var selectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == ActiveTheme) 
                                 ?? AvailableThemes.FirstOrDefault(t => t.Id == "ocean");

            var config = new HotspotConfig
            {
                SiteName = SiteName,
                WelcomeMessage = WelcomeMessage,
                WelcomeMessageV = WelcomeMessageV,
                ErbV = ErbV,
                TextSlider1 = TextSlider1,
                ImageCount = ImageCount,
                ImageV = ImageV,
                Offers = Offers,
                Estr = Estr,
                Moba = Moba,
                SupportPhone = SupportPhone,
                DeveloperName = "م/ عزيز المساح",
                DeveloperPhone = "771122633",
                ActiveTheme = ActiveTheme,
                themeHue = selectedTheme?.Hue ?? 217,
                themeS = selectedTheme?.Sat ?? 91,
                themeL = selectedTheme?.Lit ?? 60,
                SpeedOptions = SpeedOptions.ToList(),
                Packages = Packages.ToList(),
                SalesPoints = SalesPoints.ToList()
            };
            _hotspotService.SaveConfig(config);
        }

        // ── Commands ──

        [RelayCommand]
        private async Task PreviewAsync()
        {
            try
            {
                SaveToConfig();
                var config = _hotspotService.LoadConfig();

                bool isWebView2Available = false;
                try
                {
                    var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                    isWebView2Available = !string.IsNullOrEmpty(version);
                }
                catch { }

                if (isWebView2Available)
                {
                    UploadStatus = "جاري تجهيز ملفات المعاينة الآمنة بالذاكرة...";
                    var files = await _hotspotService.GetPreviewFilesAsync(config);

                    _dispatcherService.Invoke(() =>
                    {
                        var previewWindow = new Views.SecurePreviewWindow(files);
                        if (Application.Current != null && Application.Current.MainWindow != null)
                        {
                            previewWindow.Owner = Application.Current.MainWindow;
                        }
                        UploadStatus = "تم فتح المعاينة الآمنة المدمجة.";
                        previewWindow.ShowDialog();
                    });
                    return;
                }

                // Fallback to external browser if WebView2 is missing on client machine
                UploadStatus = "WebView2 غير متاح، جاري التراجع للمعاينة بالمتصفح الخارجي...";
                await FallbackExternalPreviewAsync(config);
            }
            catch (Exception ex)
            {
                UploadStatus = $"فشل المعاينة الآمنة: {ex.Message}";
                try
                {
                    var config = _hotspotService.LoadConfig();
                    await FallbackExternalPreviewAsync(config);
                }
                catch (Exception fallbackEx)
                {
                    MessageBox.Show($"فشل بدء المعاينة بالمتصفح الخارجي: {fallbackEx.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task FallbackExternalPreviewAsync(HotspotConfig config)
        {
            var tempDir = await _hotspotService.PreparePreviewFolderAsync(config);
            var indexPath = Path.Combine(tempDir, "ALFA", "index.html");

            if (File.Exists(indexPath))
            {
                UploadStatus = "تم فتح المعاينة بالمتصفح الخارجي.";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(indexPath)
                {
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        private async Task UploadConfigOnlyAsync()
        {
            if (!_activeRouterContext.IsConnected || _activeRouterContext.CurrentRouter == null)
            {
                MessageBox.Show("الرجاء الاتصال بالراوتر أولاً لتتمكن من تحديث البيانات.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsUploading = true;
            UploadProgress = 0;
            UploadStatus = "جاري تحديث ملف البيانات (config.js)...";
            _uploadCts = new CancellationTokenSource();

            try
            {
                SaveToConfig();
                var config = _hotspotService.LoadConfig();
                var router = _activeRouterContext.CurrentRouter;

                var password = string.Empty;
                if (!string.IsNullOrEmpty(router.EncryptedPassword))
                {
                    password = _secureStorageService.Decrypt(router.EncryptedPassword);
                }

                UploadProgress = 50;

                var result = await _hotspotService.UploadConfigOnlyAsync(
                    router.Host,
                    router.Username,
                    password,
                    config,
                    DestinationPath,
                    _uploadCts.Token);

                if (result.IsSuccess)
                {
                    UploadProgress = 100;
                    UploadStatus = "✅ تم تحديث بيانات وصفحة الهوتسبوت (config.js) بنجاح!";
                    MessageBox.Show($"تم رفع وتحديث الإعدادات بنجاح إلى المجلد ({DestinationPath}) بالروتر!", "تم التحديث", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    UploadStatus = $"فشل التحديث: {result.ErrorMessage}";
                    MessageBox.Show($"فشل تحديث الإعدادات بالراوتر:\n{result.ErrorMessage}", "خطأ أثناء التحديث", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                UploadStatus = $"خطأ: {ex.Message}";
                MessageBox.Show($"حدث خطأ غير متوقع أثناء التحديث:\n{ex.Message}", "خطأ غير متوقع", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsUploading = false;
                _uploadCts = null;
            }
        }

        [RelayCommand]
        private async Task PickRouterFolderAsync()
        {
            if (!_activeRouterContext.IsConnected || _activeRouterContext.CurrentRouter == null)
            {
                MessageBox.Show("الرجاء الاتصال بالراوتر أولاً لاستعراض المجلدات.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var router = _activeRouterContext.CurrentRouter;
                var password = string.Empty;
                if (!string.IsNullOrEmpty(router.EncryptedPassword))
                {
                    password = _secureStorageService.Decrypt(router.EncryptedPassword);
                }

                UploadStatus = "جاري استعراض مجلدات الراوتر...";
                var folders = await _hotspotService.GetRouterFoldersFtpAsync(router.Host, router.Username, password);

                _dispatcherService.Invoke(() =>
                {
                    var dialog = new Views.HotspotFolderPickerDialogWindow(folders, DestinationPath);
                    if (Application.Current != null && Application.Current.MainWindow != null)
                    {
                        dialog.Owner = Application.Current.MainWindow;
                    }
                    if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedFolder))
                    {
                        DestinationPath = dialog.SelectedFolder.Trim();
                        _settingsService.Set("Hotspot_DestinationPath", DestinationPath);
                        UploadStatus = $"تم اختيار المجلد ({DestinationPath}) وحفظه بنجاح.";
                        _ = LoadConfigAndSyncAsync();
                    }
                    else
                    {
                        UploadStatus = "تم إلغاء اختيار المجلد.";
                    }
                });
            }
            catch (Exception ex)
            {
                UploadStatus = $"فشل جلب مجلدات الراوتر: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CancelUpload()
        {
            if (_uploadCts != null)
            {
                _uploadCts.Cancel();
                UploadStatus = "جاري إلغاء العملية...";
            }
        }

        // ── List Management Commands ──

        [RelayCommand]
        private void AddPackage()
        {
            if (string.IsNullOrWhiteSpace(NewPackageValidity))
            {
                MessageBox.Show("الرجاء إدخال صلاحية الباقة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Packages.Add(new HotspotPackageDto
            {
                Vl = NewPackageValidity,
                Time = NewPackageTime ?? string.Empty,
                Size = NewPackageSize ?? string.Empty,
                Price = NewPackagePrice ?? string.Empty
            });

            // Reset fields
            NewPackageValidity = string.Empty;
            NewPackageTime = string.Empty;
            NewPackageSize = string.Empty;
            NewPackagePrice = string.Empty;
            SaveToConfig();
        }

        [RelayCommand]
        private void RemovePackage(HotspotPackageDto pkg)
        {
            if (pkg != null)
            {
                Packages.Remove(pkg);
                SaveToConfig();
            }
        }

        [RelayCommand]
        private void AddSpeed()
        {
            if (string.IsNullOrWhiteSpace(NewSpeedLabel))
            {
                MessageBox.Show("الرجاء إدخال اسم السرعة المعروض.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SpeedOptions.Add(new SpeedOptionDto
            {
                Label = NewSpeedLabel,
                Value = NewSpeedValue ?? string.Empty,
                Selected = false
            });

            NewSpeedLabel = string.Empty;
            NewSpeedValue = string.Empty;
            SaveToConfig();
        }

        [RelayCommand]
        private void RemoveSpeed(SpeedOptionDto speed)
        {
            if (speed != null)
            {
                SpeedOptions.Remove(speed);
                SaveToConfig();
            }
        }

        [RelayCommand]
        private void SetSpeedDefault(SpeedOptionDto speed)
        {
            if (speed != null)
            {
                foreach (var opt in SpeedOptions)
                {
                    opt.Selected = (opt == speed);
                }
                // Refresh binding
                var temp = SpeedOptions.ToList();
                SpeedOptions.Clear();
                foreach (var t in temp)
                    SpeedOptions.Add(t);
                SaveToConfig();
            }
        }

        [RelayCommand]
        private void AddSalesPoint()
        {
            if (string.IsNullOrWhiteSpace(NewSalesPoint))
            {
                MessageBox.Show("الرجاء إدخال اسم نقطة البيع.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SalesPoints.Add(NewSalesPoint);
            NewSalesPoint = string.Empty;
            SaveToConfig();
        }

        [RelayCommand]
        private void RemoveSalesPoint(string pt)
        {
            if (pt != null)
            {
                SalesPoints.Remove(pt);
                SaveToConfig();
            }
        }

        private bool _isSyncingConfig;

        private async Task LoadConfigAndSyncAsync(bool allowInteractiveFolderPicker = false)
        {
            if (_isSyncingConfig) return;
            _isSyncingConfig = true;

            try
            {
                // 1. Load local configuration first
                LoadFromConfig();

                // 2. If active router is connected, check for remote config.js
                if (_activeRouterContext.IsConnected && _activeRouterContext.CurrentRouter != null)
                {
                    var router = _activeRouterContext.CurrentRouter;
                    var password = string.Empty;
                    if (!string.IsNullOrEmpty(router.EncryptedPassword))
                    {
                        password = _secureStorageService.Decrypt(router.EncryptedPassword);
                    }

                    UploadStatus = "جاري التحقق من وجود إعدادات سابقة بالراوتر...";

                    var remoteFilePath = $"{DestinationPath}/config.js";
                    var content = await _hotspotService.DownloadFileFtpAsync(router.Host, router.Username, password, remoteFilePath);

                    if (!string.IsNullOrEmpty(content))
                    {
                        var jsonStart = content.IndexOf('{');
                        var jsonEnd = content.LastIndexOf('}');
                        if (jsonStart >= 0 && jsonEnd > jsonStart)
                        {
                            var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                            var remoteConfig = JsonSerializer.Deserialize<HotspotConfig>(json, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (remoteConfig != null)
                            {
                                SiteName = remoteConfig.SiteName;
                                WelcomeMessage = remoteConfig.WelcomeMessage;
                                WelcomeMessageV = remoteConfig.WelcomeMessageV;
                                ErbV = remoteConfig.ErbV;
                                TextSlider1 = remoteConfig.TextSlider1;
                                ImageCount = remoteConfig.ImageCount;
                                ImageV = remoteConfig.ImageV;
                                Offers = remoteConfig.Offers;
                                Estr = remoteConfig.Estr;
                                Moba = remoteConfig.Moba;
                                SupportPhone = remoteConfig.SupportPhone;
                                DeveloperName = "م/ عزيز المساح";
                                DeveloperPhone = "771122633";
                                ActiveTheme = remoteConfig.ActiveTheme;

                                SpeedOptions.Clear();
                                foreach (var opt in remoteConfig.SpeedOptions)
                                    SpeedOptions.Add(opt);

                                Packages.Clear();
                                foreach (var pkg in remoteConfig.Packages)
                                    Packages.Add(pkg);

                                SalesPoints.Clear();
                                foreach (var pt in remoteConfig.SalesPoints)
                                    SalesPoints.Add(pt);

                                HasValidRouterConfig = true;
                                UploadStatus = $"تم تحميل ومزامنة الإعدادات من المجلد ({DestinationPath}) بالراوتر بنجاح.";
                                return;
                            }
                        }
                    }

                    if (!allowInteractiveFolderPicker)
                    {
                        UploadStatus = $"لم يتم العثور على config.js في المجلد ({DestinationPath}). يمكنك تحديد المجلد يدوياً عبر زر 'تحديد مجلد الهوتسبوت'.";
                        return;
                    }

                    // If config.js was NOT found in the current DestinationPath, pop up the folder picker dialog!
                    UploadStatus = $"لم يتم العثور على config.js في المجلد ({DestinationPath}). جاري فتح نافذة اختيار المجلد...";

                    var availableFolders = await _hotspotService.GetRouterFoldersFtpAsync(router.Host, router.Username, password);

                    bool folderSelected = false;
                    _dispatcherService.Invoke(() =>
                    {
                        var dialog = new Views.HotspotFolderPickerDialogWindow(availableFolders, DestinationPath);
                        if (Application.Current != null && Application.Current.MainWindow != null)
                        {
                            dialog.Owner = Application.Current.MainWindow;
                        }
                        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedFolder))
                        {
                            DestinationPath = dialog.SelectedFolder.Trim();
                            _settingsService.Set("Hotspot_DestinationPath", DestinationPath);
                            folderSelected = true;
                        }
                    });

                    if (folderSelected)
                    {
                        // Retry fetching from selected folder
                        var newRemotePath = $"{DestinationPath}/config.js";
                        var newContent = await _hotspotService.DownloadFileFtpAsync(router.Host, router.Username, password, newRemotePath);
                        if (!string.IsNullOrEmpty(newContent))
                        {
                            var jsonStart = newContent.IndexOf('{');
                            var jsonEnd = newContent.LastIndexOf('}');
                            if (jsonStart >= 0 && jsonEnd > jsonStart)
                            {
                                var json = newContent.Substring(jsonStart, jsonEnd - jsonStart + 1);
                                var remoteConfig = JsonSerializer.Deserialize<HotspotConfig>(json, new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });

                                if (remoteConfig != null)
                                {
                                    SiteName = remoteConfig.SiteName;
                                    WelcomeMessage = remoteConfig.WelcomeMessage;
                                    WelcomeMessageV = remoteConfig.WelcomeMessageV;
                                    ErbV = remoteConfig.ErbV;
                                    TextSlider1 = remoteConfig.TextSlider1;
                                    ImageCount = remoteConfig.ImageCount;
                                    ImageV = remoteConfig.ImageV;
                                    Offers = remoteConfig.Offers;
                                    Estr = remoteConfig.Estr;
                                    Moba = remoteConfig.Moba;
                                    SupportPhone = remoteConfig.SupportPhone;
                                    DeveloperName = "م/ عزيز المساح";
                                    DeveloperPhone = "771122633";
                                    ActiveTheme = remoteConfig.ActiveTheme;

                                    SpeedOptions.Clear();
                                    foreach (var opt in remoteConfig.SpeedOptions)
                                        SpeedOptions.Add(opt);

                                    Packages.Clear();
                                    foreach (var pkg in remoteConfig.Packages)
                                        Packages.Add(pkg);

                                    SalesPoints.Clear();
                                    foreach (var pt in remoteConfig.SalesPoints)
                                        SalesPoints.Add(pt);

                                    HasValidRouterConfig = true;
                                    UploadStatus = $"تمت المزامنة وحفظ المسار ({DestinationPath}) بنجاح.";
                                    return;
                                }
                            }
                        }
                    }

                    UploadStatus = $"تم اعتماد المسار ({DestinationPath}) وتطبيق الإعدادات الحالية.";
                }
            }
            catch (Exception ex)
            {
                UploadStatus = $"فشل جلب إعدادات الراوتر: {ex.Message}";
            }
            finally
            {
                _isSyncingConfig = false;
            }
        }

        private void InitializeThemes()
        {
            AvailableThemes.Clear();
            AvailableThemes.Add(new HotspotTheme { Id = "ocean", Name = "🌊 المحيط الكلاسيكي", Description = "أزرق، نهاري، نظيف", PrimaryColor = "#3b82f6", CardBgColor = "#ffffff", AppBgColor = "#f8fafc", Hue = 217, Sat = 91, Lit = 60 });
            AvailableThemes.Add(new HotspotTheme { Id = "midnight", Name = "🌌 منتصف الليل", Description = "داكن، بنفسجي، أنيق", PrimaryColor = "#7f5af0", CardBgColor = "#16161a", AppBgColor = "#0f0e17", Hue = 255, Sat = 83, Lit = 65 });
            AvailableThemes.Add(new HotspotTheme { Id = "emerald", Name = "🌲 غابة الزمرد", Description = "داكن، أخضر لامع", PrimaryColor = "#10b981", CardBgColor = "#0b2b26", AppBgColor = "#051f20", Hue = 160, Sat = 84, Lit = 39 });
            AvailableThemes.Add(new HotspotTheme { Id = "sunset", Name = "🌇 غروب الشمس", Description = "نهاري، برتقالي دافئ", PrimaryColor = "#f97316", CardBgColor = "#ffffff", AppBgColor = "#fffbf0", Hue = 24, Sat = 96, Lit = 53 });
            AvailableThemes.Add(new HotspotTheme { Id = "cyberpunk", Name = "⚡ سايبر بانك", Description = "أسود عميق، فلوري", PrimaryColor = "#e11d48", CardBgColor = "#18181b", AppBgColor = "#09090b", Hue = 347, Sat = 77, Lit = 50 });
            AvailableThemes.Add(new HotspotTheme { Id = "coffee", Name = "☕ القهوة الدافئة", Description = "نهاري، بني وذهبي", PrimaryColor = "#8b5a2b", CardBgColor = "#ffffff", AppBgColor = "#fdf8f5", Hue = 30, Sat = 53, Lit = 36 });
            AvailableThemes.Add(new HotspotTheme { Id = "royal", Name = "👑 الذهب الملكي", Description = "أسود فخم، أزرار ذهبية", PrimaryColor = "#d4af37", CardBgColor = "#1c1c1e", AppBgColor = "#121212", Hue = 46, Sat = 65, Lit = 52 });
            AvailableThemes.Add(new HotspotTheme { Id = "crimson", Name = "🩸 الظل القرمزي", Description = "داكن، أحمر دموي", PrimaryColor = "#dc2626", CardBgColor = "#1e1212", AppBgColor = "#140c0c", Hue = 0, Sat = 72, Lit = 51 });
            AvailableThemes.Add(new HotspotTheme { Id = "frost", Name = "❄️ الصقيع القطبي", Description = "ناصع البياض، سماوي", PrimaryColor = "#0ea5e9", CardBgColor = "#ffffff", AppBgColor = "#f0f9ff", Hue = 199, Sat = 89, Lit = 48 });
            AvailableThemes.Add(new HotspotTheme { Id = "nature", Name = "🌿 الطبيعة الزجاجية", Description = "فاتح شفاف، أخضر", PrimaryColor = "#22c55e", CardBgColor = "#ffffff", AppBgColor = "#f4fbf7", Hue = 142, Sat = 71, Lit = 45 });
            AvailableThemes.Add(new HotspotTheme { Id = "sakura", Name = "🌸 زهر الساكورا", Description = "وردي ناعم، نهاري", PrimaryColor = "#fb7185", CardBgColor = "#ffffff", AppBgColor = "#fff1f2", Hue = 351, Sat = 95, Lit = 71 });
            AvailableThemes.Add(new HotspotTheme { Id = "amethyst", Name = "🔮 حجر الجمشت", Description = "بنفسجي صافي، نهاري", PrimaryColor = "#a855f7", CardBgColor = "#ffffff", AppBgColor = "#f3e8ff", Hue = 271, Sat = 91, Lit = 65 });
            AvailableThemes.Add(new HotspotTheme { Id = "aqua", Name = "💧 أكوا نيون", Description = "سماوي فلوري، داكن", PrimaryColor = "#06b6d4", CardBgColor = "#164e63", AppBgColor = "#083344", Hue = 189, Sat = 94, Lit = 43 });
            AvailableThemes.Add(new HotspotTheme { Id = "deepocean", Name = "🐋 أعماق المحيط", Description = "أزرق داكن جداً، هادئ", PrimaryColor = "#38bdf8", CardBgColor = "#0f172a", AppBgColor = "#020617", Hue = 198, Sat = 93, Lit = 60 });
            AvailableThemes.Add(new HotspotTheme { Id = "monochrome", Name = "🎱 أبيض وأسود", Description = "رمادي، احترافي", PrimaryColor = "#27272a", CardBgColor = "#ffffff", AppBgColor = "#f4f4f5", Hue = 240, Sat = 4, Lit = 16 });
            AvailableThemes.Add(new HotspotTheme { Id = "sunsetglow", Name = "🌋 وهج الغروب", Description = "تدرج ناري، داكن", PrimaryColor = "#f97316", CardBgColor = "#3f0f0f", AppBgColor = "#2a0a0a", Hue = 24, Sat = 96, Lit = 53 });
        }
    }

    public class HotspotTheme
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PrimaryColor { get; set; } = string.Empty;
        public string CardBgColor { get; set; } = string.Empty;
        public string AppBgColor { get; set; } = string.Empty;
        public int Hue { get; set; }
        public int Sat { get; set; }
        public int Lit { get; set; }
    }
}
