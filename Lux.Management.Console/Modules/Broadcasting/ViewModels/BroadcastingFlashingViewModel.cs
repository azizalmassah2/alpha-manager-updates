using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace Lux.Management.Console.Modules.Broadcasting.ViewModels
{
    public class FirmwareDevice
    {
        public string Brand  { get; set; } = string.Empty;
        public string Model  { get; set; } = string.Empty;
        public string Notes  { get; set; } = string.Empty;
        public string DisplayName => $"{Brand} — {Model}";
    }

    public partial class BroadcastingFlashingViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<FirmwareDevice> _firmwareDevices = new();

        [ObservableProperty]
        private FirmwareDevice? _selectedFirmwareDevice;

        [ObservableProperty]
        private string _targetIp = "192.168.1.1";

        [ObservableProperty]
        private string _selectedFirmwarePath = string.Empty;

        [ObservableProperty]
        private bool _useServerSource;

        [ObservableProperty]
        private string _serverUrl = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "اختر الجهاز وحدد مصدر الفلاش للبدء.";

        [ObservableProperty]
        private bool _isFlashing;

        [RelayCommand]
        private void BrowseLocalFile()
        {
            var dialog = new OpenFileDialog
            {
                Title  = "اختر ملف الفيرموير",
                Filter = "Firmware Files|*.bin;*.img;*.tar;*.gz;*.zip|All Files|*.*"
            };
            if (dialog.ShowDialog() == true)
                SelectedFirmwarePath = dialog.FileName;
        }

        private void LoadDefaultDevices()
        {
            FirmwareDevices = new ObservableCollection<FirmwareDevice>
            {
                new() { Brand = "TP-Link",  Model = "WR840N v6",        Notes = "OpenWrt 22.03" },
                new() { Brand = "TP-Link",  Model = "WR841N v14",       Notes = "OpenWrt 22.03" },
                new() { Brand = "TP-Link",  Model = "CPE210 v3",        Notes = "OpenWrt 22.03" },
                new() { Brand = "TP-Link",  Model = "CPE510 v3",        Notes = "OpenWrt 22.03" },
                new() { Brand = "TP-Link",  Model = "EAP225 v3",        Notes = "OpenWrt 22.03" },
                new() { Brand = "Ubiquiti", Model = "NanoStation M2",   Notes = "AirOS / OpenWrt" },
                new() { Brand = "Ubiquiti", Model = "NanoStation M5",   Notes = "AirOS / OpenWrt" },
                new() { Brand = "Ubiquiti", Model = "LiteBeam M5",      Notes = "AirOS" },
                new() { Brand = "Ubiquiti", Model = "PowerBeam M5",     Notes = "AirOS" },
                new() { Brand = "Ubiquiti", Model = "UniFi AP AC Lite", Notes = "UniFi / OpenWrt" },
                new() { Brand = "MikroTik", Model = "hAP ac2",          Notes = "RouterOS / OpenWrt" },
                new() { Brand = "MikroTik", Model = "wAP ac",           Notes = "RouterOS" },
                new() { Brand = "MikroTik", Model = "RB941-2nD (hAP)",  Notes = "RouterOS" },
                new() { Brand = "Huawei",   Model = "B310 LTE",         Notes = "HiLink" },
                new() { Brand = "Huawei",   Model = "E3372 USB Modem",  Notes = "HiLink" },
                new() { Brand = "Huawei",   Model = "B525 LTE",         Notes = "HiLink" },
                new() { Brand = "Cambium",  Model = "ePMP 1000",        Notes = "cambium OS" },
                new() { Brand = "Cambium",  Model = "Force 180",        Notes = "cambium OS" },
                new() { Brand = "Cambium",  Model = "ePMP 2000",        Notes = "cambium OS" },
                new() { Brand = "D-Link",   Model = "DIR-842",          Notes = "OpenWrt" },
                new() { Brand = "NETGEAR",  Model = "R6220",            Notes = "OpenWrt" },
            };
        }

        public BroadcastingFlashingViewModel(
            IPermissionService permissionService,
            IEventBus eventBus)
            : base(permissionService, eventBus)
        {
            LoadDefaultDevices();
        }
    }
}
