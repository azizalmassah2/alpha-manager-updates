using System.Windows.Controls;
using Lux.Management.Console.Modules.Settings.ViewModels;

namespace Lux.Management.Console.Modules.Settings.Views;

public partial class DbExplorerPage : Page
{
    // Default constructor for XAML instantiation
    public DbExplorerPage()
    {
        InitializeComponent();
    }

    // Constructor for DI
    public DbExplorerPage(DbExplorerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
