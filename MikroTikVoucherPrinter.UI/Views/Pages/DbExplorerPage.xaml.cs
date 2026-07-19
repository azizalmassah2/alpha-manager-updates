using System.Windows.Controls;

namespace MikroTikVoucherPrinter.UI.Views.Pages;

public partial class DbExplorerPage : UserControl
{
    public DbExplorerPage()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is DataGrid dg && dg.SelectedItem is System.Data.DataRowView rowView)
        {
            if (DataContext is ViewModels.Pages.DbExplorerViewModel vm)
            {
                if (vm.ShowUserReportCommand.CanExecute(rowView))
                {
                    vm.ShowUserReportCommand.Execute(rowView);
                }
            }
        }
    }
}
