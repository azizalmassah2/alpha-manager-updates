using System;
using System.Windows;
using OpenWrtProgrammerPro.Helpers;
using OpenWrtProgrammerPro.Models;
using OpenWrtProgrammerPro.Services.Interfaces;
using OpenWrtProgrammerPro.Views;

namespace OpenWrtProgrammerPro
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var licenseValidator = ServiceLocator.Instance.Resolve<ILicenseValidator>();
            var result = await licenseValidator.ValidateLicenseAsync();

            if (result.Status == LicenseStatus.Valid)
            {
                // If there's a warning (like Grace Period message), show it
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    MessageBox.Show(result.ErrorMessage, "تنبيه الترخيص", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                }

                ShutdownMode = ShutdownMode.OnMainWindowClose;
                ShowMainWindow();
            }
            else if (result.Status == LicenseStatus.TimeManipulation || result.Status == LicenseStatus.IntegrityViolation)
            {
                MessageBox.Show(result.ErrorMessage, "خطأ في حماية الترخيص", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                Shutdown();
            }
            else
            {
                // Show Activation Window
                var activationWindow = new ActivationWindow();
                bool? activated = activationWindow.ShowDialog();

                if (activated == true)
                {
                    ShutdownMode = ShutdownMode.OnMainWindowClose;
                    ShowMainWindow();
                }
                else
                {
                    Shutdown();
                }
            }
        }

        private void ShowMainWindow()
        {
            var mainWindow = new MainWindow();
            this.MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}
