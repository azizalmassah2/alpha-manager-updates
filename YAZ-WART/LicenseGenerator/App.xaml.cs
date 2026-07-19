using System;
using System.Windows;

namespace LicenseGenerator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var passwordWindow = new PasswordWindow();
            bool? authenticated = passwordWindow.ShowDialog();

            if (authenticated == true)
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                var mainWindow = new MainWindow();
                this.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
