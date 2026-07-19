using System.Windows;
using System.Windows.Controls;
using Lux.Management.Console.Modules.MikroTik.Connections.ViewModels;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Views;

public partial class RouterDetailsWindow : Window
{
    public RouterDetailsWindow(RouterDetailsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Link the close action
        viewModel.CloseAction = new System.Action(() => this.Close());
    }

    private void PasswordBoxControl_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is RouterDetailsViewModel vm)
        {
            vm.Password = PasswordBoxControl.Password;
        }
    }
}
