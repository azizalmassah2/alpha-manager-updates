using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Lux.Management.Console.Views
{
    public partial class AboutPage : UserControl
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch { }
        }
    }
}
