using System.Windows.Controls;
using System.Windows.Input;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Views;

public partial class NocPage : UserControl
{
    public NocPage()
    {
        InitializeComponent();
    }

    private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.Item is VlanMonitorItem vlanItem)
        {
            if (DataContext is NocViewModel viewModel)
            {
                _ = viewModel.ConfigureMonitoringCommand.ExecuteAsync(vlanItem);
            }
        }
    }
}
