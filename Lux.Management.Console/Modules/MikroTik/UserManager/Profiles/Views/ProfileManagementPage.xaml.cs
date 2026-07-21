using System.Windows;
using System.Windows.Controls;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Profiles.Views
{
    public partial class ProfileManagementPage : UserControl
    {
        public ProfileManagementPage()
        {
            InitializeComponent();
        }

        private void ProfilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.ProfileManagementViewModel vm)
            {
                vm.SelectedProfileIds.Clear();
                vm.SelectedProfileNames.Clear();
                foreach (var item in ProfilesDataGrid.SelectedItems)
                {
                    if (item is ViewModels.ProfileModel row)
                    {
                        vm.SelectedProfileIds.Add(row.Id);
                        vm.SelectedProfileNames.Add(row.Name);
                    }
                }
                vm.SelectedCount = vm.SelectedProfileIds.Count;
            }
        }

        private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                if (cb.IsChecked == true)
                {
                    ProfilesDataGrid.SelectAll();
                }
                else
                {
                    ProfilesDataGrid.UnselectAll();
                }
            }
        }

        private void BtnRowMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
    }
}
