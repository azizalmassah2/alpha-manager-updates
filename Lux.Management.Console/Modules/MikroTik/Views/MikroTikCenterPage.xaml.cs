using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Lux.Management.Console.Modules.MikroTik.ViewModels;

namespace Lux.Management.Console.Modules.MikroTik.Views
{
    public partial class MikroTikCenterPage : UserControl
    {
        public MikroTikCenterPage()
        {
            InitializeComponent();
            // [PHASE-2] الانتقال الافتراضي يحدث بعد ظهور الواجهة كاملاً
            this.Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // يُنفَّذ مرة واحدة فقط عند فتح الصفحة لأول مرة
            this.Loaded -= OnPageLoaded;

            if (DataContext is MikroTikCenterViewModel vm)
            {
                bool wasOpenRequested = vm.IsNavPanelOpen;
                if (vm.SelectedSection == null)
                {
                    // الانتقال للـ User Manager افتراضياً
                    var defaultLanding = vm.NavigationTree.FirstOrDefault(n => n.Title == "User Manager");
                    if (defaultLanding != null)
                        vm.SelectedSection = defaultLanding;
                    else if (vm.NavigationTree.Count > 0)
                        vm.SelectedSection = vm.NavigationTree[0];
                }

                // الحفاظ على السايد بار مفتوحاً إذا تم طلبه عند النقر
                if (wasOpenRequested)
                {
                    vm.IsNavPanelOpen = true;
                }
            }
        }
    }
}
