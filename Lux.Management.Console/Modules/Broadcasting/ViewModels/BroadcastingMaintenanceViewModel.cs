using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Lux.Management.Console.Modules.Broadcasting.ViewModels
{
    public partial class BroadcastingMaintenanceViewModel : ViewModelBase, IActivatable
    {
        private readonly IBroadcastingService _broadcastingService;

        [ObservableProperty]
        private ObservableCollection<BroadcastingDevice> _allDevices = new();

        [ObservableProperty]
        private ObservableCollection<BroadcastingDevice> _filteredDevices = new();

        [ObservableProperty]
        private BroadcastingDevice? _selectedDevice;

        [ObservableProperty]
        private string _selectedVendorFilter = "الكل";

        [ObservableProperty]
        private ObservableCollection<string> _vendorFilters = new() { "الكل" };

        // حقول نموذج التعديل
        [ObservableProperty] private string _editDisplayName = string.Empty;
        [ObservableProperty] private string _editIpAddress   = string.Empty;
        [ObservableProperty] private string _editMacAddress  = string.Empty;
        [ObservableProperty] private string _editDeviceType  = "Antenna";
        [ObservableProperty] private string _editVendor      = string.Empty;
        [ObservableProperty] private string _editUsername    = "admin";
        [ObservableProperty] private string _editPassword    = string.Empty;
        [ObservableProperty] private string _editNotes       = string.Empty;

        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool   _isBusy;
        [ObservableProperty] private string _pingResult = string.Empty;

        partial void OnSelectedDeviceChanged(BroadcastingDevice? value)
        {
            if (value == null) return;
            EditDisplayName = value.DisplayName;
            EditIpAddress   = value.IpAddress;
            EditMacAddress  = value.MacAddress;
            EditDeviceType  = value.DeviceType;
            EditVendor      = value.Vendor;
            EditUsername    = value.Username;
            EditPassword    = value.Password;
            EditNotes       = value.Notes;
        }

        partial void OnSelectedVendorFilterChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var filtered = SelectedVendorFilter == "الكل"
                ? AllDevices.ToList()
                : AllDevices.Where(d => d.Vendor == SelectedVendorFilter).ToList();
            FilteredDevices = new ObservableCollection<BroadcastingDevice>(filtered);
        }

        [RelayCommand]
        private async Task LoadDevicesAsync()
        {
            IsBusy = true;
            try
            {
                var devices = await _broadcastingService.GetAllDevicesAsync();
                AllDevices = new ObservableCollection<BroadcastingDevice>(devices);
                var vendors = devices.Select(d => d.Vendor).Where(v => !string.IsNullOrEmpty(v))
                                     .Distinct().OrderBy(v => v).ToList();
                VendorFilters = new ObservableCollection<string>(new[] { "الكل" }.Concat(vendors));
                ApplyFilter();
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SaveDeviceAsync()
        {
            if (string.IsNullOrWhiteSpace(EditDisplayName) || string.IsNullOrWhiteSpace(EditIpAddress))
            {
                StatusMessage = "⚠️ يرجى إدخال الاسم وعنوان IP على الأقل.";
                return;
            }
            IsBusy = true;
            try
            {
                if (SelectedDevice == null)
                {
                    await _broadcastingService.AddDeviceAsync(new BroadcastingDevice
                    {
                        DisplayName = EditDisplayName, IpAddress = EditIpAddress,
                        MacAddress  = EditMacAddress,  DeviceType = EditDeviceType,
                        Vendor      = EditVendor,      Username   = EditUsername,
                        Password    = EditPassword,    Notes      = EditNotes
                    });
                    StatusMessage = "✅ تم إضافة الجهاز بنجاح.";
                }
                else
                {
                    SelectedDevice.DisplayName = EditDisplayName;
                    SelectedDevice.IpAddress   = EditIpAddress;
                    SelectedDevice.MacAddress  = EditMacAddress;
                    SelectedDevice.DeviceType  = EditDeviceType;
                    SelectedDevice.Vendor      = EditVendor;
                    SelectedDevice.Username    = EditUsername;
                    SelectedDevice.Password    = EditPassword;
                    SelectedDevice.Notes       = EditNotes;
                    await _broadcastingService.UpdateDeviceAsync(SelectedDevice);
                    StatusMessage = "✅ تم تحديث الجهاز بنجاح.";
                }
                await LoadDevicesAsync();
                NewDevice();
            }
            catch (Exception ex) { StatusMessage = $"❌ خطأ: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task DeleteDeviceAsync()
        {
            if (SelectedDevice == null) return;
            IsBusy = true;
            try
            {
                await _broadcastingService.DeleteDeviceAsync(SelectedDevice.Id);
                StatusMessage = "✅ تم حذف الجهاز بنجاح.";
                await LoadDevicesAsync();
                NewDevice();
            }
            catch (Exception ex) { StatusMessage = $"❌ خطأ: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void NewDevice()
        {
            SelectedDevice  = null;
            EditDisplayName = EditIpAddress = EditMacAddress = EditVendor = EditPassword = EditNotes = string.Empty;
            EditDeviceType  = "Antenna";
            EditUsername    = "admin";
            PingResult      = string.Empty;
        }

        [RelayCommand]
        private async Task PingDeviceAsync()
        {
            if (string.IsNullOrEmpty(EditIpAddress)) { PingResult = "أدخل عنوان IP أولاً."; return; }
            try
            {
                var reply = await new Ping().SendPingAsync(EditIpAddress, 2000);
                PingResult = reply.Status == IPStatus.Success
                    ? $"✅ يستجيب — {reply.RoundtripTime} ms"
                    : "❌ لا يستجيب";
            }
            catch (Exception ex) { PingResult = $"خطأ: {ex.Message}"; }
        }

        [RelayCommand]
        private void OpenWebUi()
        {
            if (string.IsNullOrEmpty(EditIpAddress)) return;
            Process.Start(new ProcessStartInfo($"http://{EditIpAddress}") { UseShellExecute = true });
        }

        public BroadcastingMaintenanceViewModel(
            IPermissionService permissionService,
            IEventBus eventBus,
            IBroadcastingService broadcastingService)
            : base(permissionService, eventBus)
        {
            _broadcastingService = broadcastingService;
        }

        public async Task ActivateAsync() => await LoadDevicesAsync();
    }
}
