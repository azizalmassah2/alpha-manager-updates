using System.Windows;
using HandyControl.Controls;
using HandyControl.Data;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Services;

public class UserNotificationService : IUserNotificationService
{
    public void ShowError(string message, string? title = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Growl.Error(new GrowlInfo
            {
                Message = message,
                WaitTime = 5,
                ShowDateTime = false,
                ConfirmStr = "إغلاق",
                CancelStr = "إلغاء",
            });
        });
    }

    public void ShowWarning(string message, string? title = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Growl.Warning(new GrowlInfo
            {
                Message = message,
                WaitTime = 4,
                ShowDateTime = false
            });
        });
    }

    public void ShowSuccess(string message, string? title = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Growl.Success(new GrowlInfo
            {
                Message = message,
                WaitTime = 3,
                ShowDateTime = false
            });
        });
    }

    public void ShowInformation(string message, string? title = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Growl.Info(new GrowlInfo
            {
                Message = message,
                WaitTime = 3,
                ShowDateTime = false
            });
        });
    }
}
