using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;

namespace MikroTikVoucherPrinter.UI.ViewModels.Pages;

public partial class ProfileManagementViewModel : BaseViewModel
{
    private readonly IProfileService _profileService;

    public ObservableCollection<Profile> Profiles { get; } = new();

    public ProfileManagementViewModel(
        IProfileService profileService,
        ILogger<ProfileManagementViewModel> logger) : base(logger)
    {
        _profileService = profileService;
        Title = "إدارة الباقات";

        LoadCommand = new AsyncRelayCommand(LoadProfilesAsync);
        AddCommand = new AsyncRelayCommand(AddProfileAsync, CanAddProfile);
        SaveEditCommand = new AsyncRelayCommand(SaveEditAsync);
        CancelEditCommand = new RelayCommand(CancelEdit);
        EditCommand = new RelayCommand<Profile>(EditProfile);
        DeleteCommand = new AsyncRelayCommand<Profile>(DeleteProfileAsync);
    }

    private string _newName = "";
    public string NewName { get => _newName; set { SetProperty(ref _newName, value); AddCommand?.NotifyCanExecuteChanged(); SaveEditCommand?.NotifyCanExecuteChanged(); } }

    private int _newDurationDays = 30;
    public int NewDurationDays { get => _newDurationDays; set => SetProperty(ref _newDurationDays, value); }

    private int _newTransferValue = 1;
    public int NewTransferValue { get => _newTransferValue; set => SetProperty(ref _newTransferValue, value); }

    public List<string> TransferUnits { get; } = new List<string> { "MB", "GB" };
    private string _selectedTransferUnit = "GB";
    public string SelectedTransferUnit { get => _selectedTransferUnit; set => SetProperty(ref _selectedTransferUnit, value); }

    private int _newUptimeHours = 24;
    public int NewUptimeHours { get => _newUptimeHours; set => SetProperty(ref _newUptimeHours, value); }

    // RateLimit remains string (e.g. 2M/2M)
    private string _newRate = "";
    public string NewRate { get => _newRate; set => SetProperty(ref _newRate, value); }

    public List<int> SharedUsersList { get; } = new List<int> { 1, 2, 3, 4, 5, 10 };
    private int _newSharedUsers = 1;
    public int NewSharedUsers { get => _newSharedUsers; set => SetProperty(ref _newSharedUsers, value); }

    private decimal _newPrice = 1000;
    public decimal NewPrice { get => _newPrice; set => SetProperty(ref _newPrice, value); }

    private bool _isNameEnabled = true;
    public bool IsNameEnabled { get => _isNameEnabled; set => SetProperty(ref _isNameEnabled, value); }
    
    private bool _isEditMode;
    public bool IsEditMode 
    { 
        get => _isEditMode; 
        set 
        { 
            SetProperty(ref _isEditMode, value);
            OnPropertyChanged(nameof(AddVisibility));
            OnPropertyChanged(nameof(EditVisibility));
            IsNameEnabled = !value; // Lock name input when editing
        } 
    }

    public System.Windows.Visibility AddVisibility => IsEditMode ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    public System.Windows.Visibility EditVisibility => IsEditMode ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand AddCommand { get; }
    public IAsyncRelayCommand SaveEditCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IRelayCommand<Profile> EditCommand { get; }
    public IAsyncRelayCommand<Profile> DeleteCommand { get; }

    public override async Task InitializeAsync(object? parameter = null)
    {
        await LoadProfilesAsync();
    }

    private void EditProfile(Profile? profile)
    {
        if (profile == null) return;
        
        try 
        {
            NewName = profile.Name ?? "";
            NewPrice = profile.Price;
            
            // Parse basic values safely to prevent NullReferenceExceptions
            NewDurationDays = int.TryParse(profile.Duration?.Replace("d","") ?? "", out int d) ? d : 0;
            
            if (!string.IsNullOrEmpty(profile.Transfer) && profile.Transfer.Contains(" ")) {
                var parts = profile.Transfer.Split(' ');
                if(parts.Length == 2 && int.TryParse(parts[0], out int v)) {
                    NewTransferValue = v;
                    SelectedTransferUnit = parts[1];
                }
            }
            else {
                NewTransferValue = 0;
                SelectedTransferUnit = "GB"; // Default reset
            }
            
            NewUptimeHours = int.TryParse(profile.Uptime?.Replace("h","") ?? "", out int h) ? h : 0;
            NewSharedUsers = int.TryParse(profile.SharedUsers, out int su) && su > 0 ? su : 1;
            
            IsEditMode = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء فك تشفير بيانات الباقة للتعديل:\n" + ex.Message, "خطأ داخلي", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void CancelEdit()
    {
        IsEditMode = false;
        NewName = string.Empty;
    }

    private async Task SaveEditAsync()
    {
        var result = System.Windows.MessageBox.Show($"هل أنت متأكد من حفظ التعديلات على الباقة {NewName}؟", "تأكيد التعديل", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        await ExecuteBusyAsync(async (token) =>
        {
            try 
            {
                string duration = NewDurationDays > 0 ? $"{NewDurationDays}d" : "";
                string transfer = "";
                string displayTransfer = "";
                if (NewTransferValue > 0)
                {
                    long bytes = SelectedTransferUnit == "GB" ? (long)NewTransferValue * 1024 * 1024 * 1024 : (long)NewTransferValue * 1024 * 1024;
                    transfer = bytes.ToString();
                    displayTransfer = $"{NewTransferValue} {SelectedTransferUnit}";
                }
                string uptime = NewUptimeHours > 0 ? $"{NewUptimeHours}h" : "";
                string sharedUsers = NewSharedUsers.ToString();

                await _profileService.UpdateProfileAsync(MikroTikVoucherPrinter.Domain.Enums.PackageSourceType.UserManager, NewName, duration, transfer, uptime, sharedUsers, NewPrice, token);
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var p = Profiles.FirstOrDefault(x => x.Name == NewName);
                    if (p != null) {
                        p.Price = NewPrice;
                        p.Duration = duration;
                        p.Transfer = displayTransfer;
                        p.Uptime = uptime;
                        
                        // Trick to refresh DataGrid without re-fetching everything
                        int i = Profiles.IndexOf(p);
                        Profiles.RemoveAt(i);
                        Profiles.Insert(i, p);
                    }
                    IsEditMode = false;
                    NewName = string.Empty;
                    System.Windows.MessageBox.Show("تم تعديل بيانات الباقة بنجاح!", "نجاح", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.MessageBox.Show(ex.Message, "فشل التعديل", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error));
            }
        }, "جاري حفظ التعديلات...");
    }

    private async Task LoadProfilesAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var items = await _profileService.GetAllProfilesAsync(MikroTikVoucherPrinter.Domain.Enums.PackageSourceType.UserManager, token);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Profiles.Clear();
                foreach (var item in items) Profiles.Add(item);
            });
        }, "جاري تحميل الباقات...");
    }

    private bool CanAddProfile() => !string.IsNullOrWhiteSpace(NewName);

    private async Task AddProfileAsync()
    {
        var confirm = System.Windows.MessageBox.Show($"هل أنت متأكد من إنشاء الباقة [{NewName}] وحقنها في سيرفر المايكروتك؟", "تأكيد الإنشاء", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        await ExecuteBusyAsync(async (token) =>
        {
            try 
            {
                // تحويل الحقول إلى لغة المايكروتك
                string duration = NewDurationDays > 0 ? $"{NewDurationDays}d" : "";
                
                string transfer = "";
                string displayTransfer = "";
                if (NewTransferValue > 0)
                {
                    long bytes = SelectedTransferUnit == "GB" 
                        ? (long)NewTransferValue * 1024 * 1024 * 1024 
                        : (long)NewTransferValue * 1024 * 1024;
                        
                    transfer = bytes.ToString(); // Send exact bytes to RouterOS
                    displayTransfer = $"{NewTransferValue} {SelectedTransferUnit}"; // Keep it human readable in our local Grid
                }

                string uptime = NewUptimeHours > 0 ? $"{NewUptimeHours}h" : "";
                string sharedUsers = NewSharedUsers.ToString();

                var profile = await _profileService.CreateProfileAsync(
                    MikroTikVoucherPrinter.Domain.Enums.PackageSourceType.UserManager, NewName, duration, transfer, uptime, NewRate, sharedUsers, NewPrice, "admin", token);
                
                profile.Transfer = displayTransfer; // Override raw bytes with human readable text for UI
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Profiles.Add(profile);
                    System.Windows.MessageBox.Show($"تم إنشاء وبناء الباقة {NewName} وقيودها بنجاح!", "نجاح", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    NewName = string.Empty;
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show(ex.Message, "فشل إنشاء الباقة", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
            }
        }, "جاري إنشاء الباقة والقيود في الراوتر...");
    }

    private async Task DeleteProfileAsync(Profile? profile)
    {
        if (profile == null) return;
        
        var confirm = System.Windows.MessageBox.Show($"هل أنت متأكد من رغبتك في حذف الباقة نهائياً [{profile.Name}]؟\nلا يمكن التراجع عن هذه الخطوة وسيتم مسح المحددات المرتبطة بها.", "تحذير أمني", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Error);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        await ExecuteBusyAsync(async (token) =>
        {
            await _profileService.DeleteProfileByNameAsync(MikroTikVoucherPrinter.Domain.Enums.PackageSourceType.UserManager, profile.Name, token);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Profiles.Remove(profile);
            });
            Logger.LogInformation("🗑️ تم حذف الباقة");
        }, "جاري الحذف...");
    }
}
