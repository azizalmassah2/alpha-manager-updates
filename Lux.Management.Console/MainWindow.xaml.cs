using System.Windows;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Management.Console.Modules.MikroTik.Dashboard;
using Lux.Management.Console.Views;

namespace Lux.Management.Console;

public partial class MainWindow : Window
{


    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Initial navigation is now handled by AttemptAutoReconnectAsync in MainViewModel.

        // مهم: ShutdownMode=OnExplicitShutdown — يجب استدعاء Shutdown عند إغلاق النافذة الرئيسية
        Closed += (_, _) => Application.Current.Shutdown(0);
    }

    private void GlobalConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            if (vm.ActiveRouterStatus.State == MikroTikVoucherPrinter.Domain.Enums.Platform.ConnectionState.Connected)
            {
                if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
                {
                    btn.ContextMenu.PlacementTarget = btn;
                    btn.ContextMenu.IsOpen = true;
                }
            }
            else
            {
                vm.IsConnectionDialogVisible = true;
            }
        }
    }

    private void QuickActionsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void UserAccountBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }
}