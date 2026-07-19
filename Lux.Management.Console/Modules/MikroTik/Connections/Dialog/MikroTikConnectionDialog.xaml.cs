using System.Windows;
using System.Windows.Controls;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Dialog;

public partial class MikroTikConnectionDialog : UserControl
{
    public MikroTikConnectionDialog()
    {
        InitializeComponent();
        this.DataContextChanged += MikroTikConnectionDialog_DataContextChanged;
    }

    private void MikroTikConnectionDialog_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MikroTikConnectionDialogViewModel vm)
        {
            var binding = System.Windows.Data.BindingOperations.GetBindingExpression(DiscoveryListBox, ListBox.ItemsSourceProperty);
            System.Console.WriteLine($"[DIAGNOSTIC] ItemsSource Binding Path: {binding?.ParentBinding?.Path?.Path}");
            
            vm.DiscoveredDevices.CollectionChanged += (s, ev) =>
            {
                // Ensure we log on UI thread AFTER UI updates
                this.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    System.Console.WriteLine($"[DIAGNOSTIC] ListBox Items Count = {DiscoveryListBox.Items.Count}");
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }
    }

    private void PasswordBoxControl_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MikroTikConnectionDialogViewModel vm)
        {
            vm.Password = PasswordBoxControl.Password;
        }
    }
}
