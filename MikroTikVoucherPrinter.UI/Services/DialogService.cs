using System.Windows;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.UI.Services;

/// <summary>
/// خدمة الحوارات - تغلف MessageBox لفصلها عن ViewModels
/// </summary>
public sealed class DialogService : IDialogService
{
    public Task ShowInfoAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmAsync(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    public Task<bool?> ShowConfirmWithCancelAsync(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return Task.FromResult<bool?>(result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        });
    }
}
