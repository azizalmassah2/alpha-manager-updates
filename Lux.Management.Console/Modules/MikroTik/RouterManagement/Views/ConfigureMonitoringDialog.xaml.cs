using System.Windows;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Views
{
    public partial class ConfigureMonitoringDialog : Window
    {
        public string DeviceIp { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public bool EnableMonitoring { get; private set; }

        public ConfigureMonitoringDialog(string currentDeviceIp, string currentDescription, bool currentEnableMonitoring)
        {
            InitializeComponent();
            TxtDeviceIp.Text = currentDeviceIp;
            TxtDescription.Text = currentDescription;
            ChkEnableMonitoring.IsChecked = currentEnableMonitoring;
            TxtDeviceIp.Focus();
            if (!string.IsNullOrEmpty(currentDeviceIp))
            {
                TxtDeviceIp.SelectAll();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var ip = TxtDeviceIp.Text.Trim();
            if (string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show("يرجى إدخال عنوان IP للجهاز.", "خطأ في المدخلات", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DeviceIp = ip;
            Description = TxtDescription.Text.Trim();
            EnableMonitoring = ChkEnableMonitoring.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
