using System.Windows;
using System.Windows.Controls;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Agents.Views;

public partial class AgentManagementPage : UserControl
{
    public AgentManagementPage()
    {
        InitializeComponent();
    }

    private void BtnRowMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }
}
