using System.Windows;
using System.Windows.Input;
using OpenWrtProgrammerPro.ViewModels;

namespace OpenWrtProgrammerPro.Views
{
    public partial class ScanNetworksWindow : Window
    {
        public ScanNetworksWindow()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectAndClose();
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            SelectAndClose();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SelectAndClose()
        {
            if (DataContext is ScanNetworksViewModel vm && vm.SelectedNetwork != null)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("يرجى اختيار شبكة من الجدول أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
