using System.Windows.Controls;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.ViewModels;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Views
{
    /// <summary>
    /// صفحة الضبط السريع — تضم كامل واجهة أداة برمجة أجهزة OpenWrt (منقولة من YAZ-WART)
    /// </summary>
    public partial class QuickConfigPage : UserControl
    {
        public QuickConfigPage()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
