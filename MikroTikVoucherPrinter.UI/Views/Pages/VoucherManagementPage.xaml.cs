using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MikroTikVoucherPrinter.UI.ViewModels.Pages;

namespace MikroTikVoucherPrinter.UI.Views.Pages;

public partial class VoucherManagementPage : UserControl
{
    public VoucherManagementPage()
    {
        InitializeComponent();
    }

    // نقل الكروت المحددة إلى ViewModel عند تغيير التحديد
    private void MainGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is VoucherManagementViewModel vm)
        {
            vm.SelectedCount = MainGrid.SelectedItems.Count;
            
            vm.SelectedVoucherIds.Clear();
            foreach (var item in MainGrid.SelectedItems)
            {
                if (item is Application.DTOs.VoucherDto dto)
                {
                    vm.SelectedVoucherIds.Add(dto.Id);
                }
            }
        }
    }

    private void VoucherOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { ContextMenu: { } menu })
        {
            menu.PlacementTarget = (UIElement)sender;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
