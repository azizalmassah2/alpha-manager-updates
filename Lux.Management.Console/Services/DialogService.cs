using System;
using System.Threading.Tasks;
using System.Windows;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Services;

public class DialogService : IDialogService
{
    public Task<bool> ShowConfirmationAsync(string message, string title = "تأكيد")
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    public Task ShowAlertAsync(string message, string title = "تنبيه")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public string? ShowSaveFileDialog(string title, string filter, string defaultExt, string fileName)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = fileName
        };
        if (dlg.ShowDialog() == true)
            return dlg.FileName;
        return null;
    }

    public string? ShowOpenFileDialog(string title, string filter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter
        };
        if (dlg.ShowDialog() == true)
            return dlg.FileName;
        return null;
    }
}
