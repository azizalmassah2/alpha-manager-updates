using System.Windows;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.ViewModels;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // حفظ الإعدادات عند الإغلاق بشكل متزامن وآمن لتجنب التعليق
                vm.SaveSettings();
            }
            base.OnClosing(e);
        }
    }
}
