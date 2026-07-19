using Lux.Management.Console.Modules.Broadcasting.ViewModels;
using System.Windows.Controls;

namespace Lux.Management.Console.Modules.Broadcasting.Views
{
    public partial class BroadcastingCenterPage : UserControl
    {
        public BroadcastingCenterPage()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is BroadcastingCenterViewModel vm)
            {
                // لا نفتح أي صفحة فرعية تلقائياً عند الدخول
                vm.CurrentSubPageViewModel = null;
                vm.SelectedSection = null;
                vm.SelectedSubNode = null;
                vm.ActiveSectionPills = new System.Collections.Generic.List<BroadcastingNavigationNode>();
                vm.IsPillStripVisible = false;
                vm.BreadcrumbText = "أجهزة البث";

                // نقوم بفتح لوحة الأقسام الجانبية الفرعية تلقائياً لينتظر اختيار المستخدم
                vm.IsNavPanelOpen = true;
            }
        }
    }
}
