using System.Windows;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Views
{
    public partial class PreviewWindow : Window
    {
        public PreviewWindow(ProgrammingPreview preview)
        {
            InitializeComponent();
            LoadPreview(preview);
        }

        private void LoadPreview(ProgrammingPreview preview)
        {
            TxtTargetIps.Text = preview.TargetIps;
            TxtHostnames.Text = preview.Hostnames;
            TxtGateway.Text = string.IsNullOrWhiteSpace(preview.Gateway) ? "بدون بوابة" : preview.Gateway;
            TxtSubnet.Text = preview.SubnetMask;
            TxtVlan.Text = $"vlan{preview.VlanId} (الجسر: br-lan.{preview.VlanId})";
            TxtDhcp.Text = preview.DhcpStatusText;

            TxtWifi24.Text = $"SSID: {preview.Ssid24Ghz} | التشفير: WPA2-PSK | كلمة المرور: {preview.WifiPassword}";
            TxtWifi5Mode.Text = preview.ModeText;

            if (preview.IsClientWds)
            {
                LblWifi5Details.Text = "الشبكة البعيدة المتصل بها (Client WDS):";
                TxtWifi5Details.Text = $"SSID: {preview.RemoteSsid} | كلمة المرور: {preview.RemotePassword}";
            }
            else
            {
                LblWifi5Details.Text = "شبكة 5GHz (نقطة وصول AP):";
                TxtWifi5Details.Text = $"SSID: {preview.Ssid5Ghz} | التشفير: WPA2-PSK | كلمة المرور: {preview.WifiPassword}";
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
