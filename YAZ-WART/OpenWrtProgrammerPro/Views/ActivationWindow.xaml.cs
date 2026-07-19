using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using OpenWrtProgrammerPro.Helpers;
using OpenWrtProgrammerPro.Services.Interfaces;

namespace OpenWrtProgrammerPro.Views
{
    public partial class ActivationWindow : Window
    {
        private readonly ILicenseValidator _licenseValidator;
        private readonly string _hardwareId;

        public ActivationWindow()
        {
            InitializeComponent();
            _licenseValidator = ServiceLocator.Instance.Resolve<ILicenseValidator>();
            _hardwareId = _licenseValidator.GetHardwareId();
            TxtHardwareId.Text = _hardwareId;
            TxtCustomerName.Text = "عميل لوكس كارد";
            
            UpdateQrCode();
        }

        private void UpdateQrCode()
        {
            try
            {
                string customerName = string.IsNullOrWhiteSpace(TxtCustomerName.Text) ? "عميل لوكس كارد" : TxtCustomerName.Text;
                string qrContent = $"Name: {customerName}\nHWID: {_hardwareId}";
                var bitmap = QrCodeGenerator.GenerateQrCode(qrContent, scale: 5);
                ImgQrCode.Source = bitmap;
            }
            catch
            {
                // Fallback if QR generation fails
            }
        }

        private void TxtCustomerName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateQrCode();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_hardwareId);
                MessageBox.Show("تم نسخ معرّف الجهاز بنجاح إلى الحافظة.", "نسخ المعرّف", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل النسخ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = "activation_request.txt",
                Filter = "Text Files (*.txt)|*.txt",
                Title = "تصدير طلب التفعيل"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string customerName = string.IsNullOrWhiteSpace(TxtCustomerName.Text) ? "عميل غير معروف" : TxtCustomerName.Text;
                    string content = $"Customer Name: {customerName}\n" +
                                     $"Hardware ID: {_hardwareId}\n" +
                                     $"CPU ID Hash: {HardwareIdProvider.GetCpuIdHash()}\n" +
                                     $"Board Serial Hash: {HardwareIdProvider.GetBoardSerialHash()}\n" +
                                     $"Disk Serial Hash: {HardwareIdProvider.GetDiskSerialHash()}\n" +
                                     $"MachineGuid Hash: {HardwareIdProvider.GetMachineGuidHash()}\n" +
                                     $"Computer Name: {Environment.MachineName}\n" +
                                     $"Windows User: {Environment.UserName}\n" +
                                     $"Date Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";

                    File.WriteAllText(sfd.FileName, content);
                    MessageBox.Show("تم تصدير ملف طلب التفعيل بنجاح. يرجى إرساله للمطور.", "تصدير طلب التفعيل", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل التصدير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                }
            }
        }

        private async void LoadLicense_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "License Files (license.dat)|license.dat;*.dat;*.json|All Files (*.*)|*.*",
                Title = "تحميل ملف الترخيص"
            };

            if (ofd.ShowDialog() == true)
            {
                bool success = await _licenseValidator.LoadAndActivateLicenseAsync(ofd.FileName);
                if (success)
                {
                    MessageBox.Show("تم تفعيل البرنامج بنجاح! سيتم تشغيل الواجهة الآن.", "تفعيل ناجح", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("فشل تفعيل الترخيص. يرجى التأكد من اختيار ملف ترخيص (license.dat) صالح ومطابق لمعرّف جهازك.", "فشل التفعيل", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
