using System;
using System.Windows;
using OpenWrtProgrammerPro.Helpers;
using OpenWrtProgrammerPro.Models;
using OpenWrtProgrammerPro.Services.Interfaces;

namespace OpenWrtProgrammerPro.Views
{
    public partial class LicenseInfoWindow : Window
    {
        public LicenseInfoWindow()
        {
            InitializeComponent();
            LoadLicenseInfo();
        }

        private void LoadLicenseInfo()
        {
            try
            {
                var validator = ServiceLocator.Instance.Resolve<ILicenseValidator>();
                var license = validator.ActiveLicense;
                
                if (license != null)
                {
                    TxtCustomer.Text = license.CustomerName;
                    TxtLicenseId.Text = string.IsNullOrEmpty(license.LicenseId) ? "N/A" : license.LicenseId;
                    TxtType.Text = TranslateLicenseType(license.LicenseType);
                    TxtIssueDate.Text = license.IssueDate.ToString("yyyy-MM-dd");
                    TxtExpiryDate.Text = license.ExpiryDate.ToString("yyyy-MM-dd");
                    TxtVersion.Text = $"V{license.LicenseVersion} (Key: v{license.KeyVersion})";
                    TxtHwid.Text = license.HardwareId;

                    // Remaining days calculation
                    DateTime today = DateTime.Today;
                    if (today > license.ExpiryDate)
                    {
                        int remainingGrace = license.GracePeriodDays - (today - license.ExpiryDate).Days;
                        if (remainingGrace >= 0)
                        {
                            TxtRemaining.Text = $"منتهي (فترة سماح متبقية: {remainingGrace} أيام)";
                            TxtRemaining.Foreground = System.Windows.Media.Brushes.DarkOrange;
                        }
                        else
                        {
                            TxtRemaining.Text = "منتهي الصلاحية";
                            TxtRemaining.Foreground = System.Windows.Media.Brushes.Red;
                        }
                    }
                    else
                    {
                        int remaining = (license.ExpiryDate - today).Days;
                        TxtRemaining.Text = $"{remaining} يومًا";
                    }
                }
                else
                {
                    // Fallback if no active license found in locator
                    TxtCustomer.Text = "غير مفعل";
                    TxtLicenseId.Text = "N/A";
                    TxtType.Text = "تجريبي / غير معروف";
                    TxtIssueDate.Text = "N/A";
                    TxtExpiryDate.Text = "N/A";
                    TxtRemaining.Text = "N/A";
                    TxtVersion.Text = "N/A";
                    TxtHwid.Text = validator.GetHardwareId();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء تحميل بيانات الترخيص: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
        }

        private string TranslateLicenseType(string type)
        {
            return type.ToLower() switch
            {
                "trial" => "تجريبي (Trial)",
                "monthly" => "شهري (Monthly)",
                "yearly" => "سنوي (Yearly)",
                "lifetime" => "مدى الحياة (Lifetime)",
                "custom" => "مخصص (Custom)",
                _ => type
            };
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
