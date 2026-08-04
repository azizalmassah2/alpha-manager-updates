using System.Windows.Controls;
using MikroTikVoucherPrinter.UI.ViewModels.Pages;

namespace MikroTikVoucherPrinter.UI.Views.Pages;

public partial class BatchManagementPage : UserControl
{
    public BatchManagementPage(BatchManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadBatchesAsync();
    }
}
