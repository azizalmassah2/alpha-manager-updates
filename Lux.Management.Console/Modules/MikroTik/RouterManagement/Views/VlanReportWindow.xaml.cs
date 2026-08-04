using System.Windows;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Views;

public partial class VlanReportWindow : HandyControl.Controls.Window
{
    public VlanReportWindow(VlanReportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadReportAsync();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
