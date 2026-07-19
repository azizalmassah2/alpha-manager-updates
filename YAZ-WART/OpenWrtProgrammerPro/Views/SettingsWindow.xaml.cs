using System.Windows;
using OpenWrtProgrammerPro.ViewModels;

namespace OpenWrtProgrammerPro.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SettingsViewModel oldVm)
            {
                oldVm.RequestClose -= Vm_RequestClose;
            }
            if (e.NewValue is SettingsViewModel newVm)
            {
                newVm.RequestClose += Vm_RequestClose;
            }
        }

        private void Vm_RequestClose(object? sender, bool success)
        {
            DialogResult = success;
            Close();
        }
    }
}
