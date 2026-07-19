using System;
using System.Threading.Tasks;

namespace Lux.Management.Console.Core;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string message, string title = "تأكيد");
    Task ShowAlertAsync(string message, string title = "تنبيه");

    /// <summary>
    /// Shows a Save File Dialog and returns the selected path, or null if canceled.
    /// </summary>
    string? ShowSaveFileDialog(string title, string filter, string defaultExt, string fileName);

    /// <summary>
    /// Shows an Open File Dialog and returns the selected path, or null if canceled.
    /// </summary>
    string? ShowOpenFileDialog(string title, string filter);
}
