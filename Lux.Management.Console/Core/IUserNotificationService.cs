namespace Lux.Management.Console.Core;

public interface IUserNotificationService
{
    void ShowError(string message, string? title = null);
    void ShowWarning(string message, string? title = null);
    void ShowSuccess(string message, string? title = null);
    void ShowInformation(string message, string? title = null);
}
