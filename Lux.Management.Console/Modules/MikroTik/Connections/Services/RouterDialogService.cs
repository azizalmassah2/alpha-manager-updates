using System.Threading.Tasks;
using System.Windows;
using Lux.Management.Console.Modules.MikroTik.Connections.ViewModels;
using Lux.Management.Console.Modules.MikroTik.Connections.Views;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Services;

public class RouterDialogService : IRouterDialogService
{
    public Task<Router?> ShowAddEditRouterDialogAsync(Router? existingRouter = null)
    {
        var tcs = new TaskCompletionSource<Router?>();

        Application.Current.Dispatcher.Invoke(() =>
        {
            var viewModel = new RouterDetailsViewModel(existingRouter);
            var window = new RouterDetailsWindow(viewModel);

            // Setting owner prevents it from appearing behind the main window
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                window.Owner = Application.Current.MainWindow;
            }

            window.ShowDialog();

            if (viewModel.DialogResult)
            {
                tcs.SetResult(viewModel.ResultRouter);
            }
            else
            {
                tcs.SetResult(null);
            }
        });

        return tcs.Task;
    }
}
