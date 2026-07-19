using System.Windows;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Views
{
    public partial class EditVlanNameDialog : Window
    {
        public string VlanName { get; private set; } = string.Empty;

        public EditVlanNameDialog(string currentName)
        {
            InitializeComponent();
            TxtVlanName.Text = currentName;
            TxtVlanName.Focus();
            TxtVlanName.SelectAll();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            VlanName = TxtVlanName.Text.Trim();
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
