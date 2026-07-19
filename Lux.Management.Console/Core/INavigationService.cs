using System;
using Lux.Management.Console.ViewModels;

namespace Lux.Management.Console.Core;

public interface INavigationService
{
    void Navigate<TViewModel>() where TViewModel : ViewModelBase;
    void NavigateToProject(Guid projectId);
    void NavigateToDevice(Guid deviceId);
    void NavigateToOperation(Guid operationId);
    void NavigateToAlert(Guid alertId);
    void GoBack();
    bool CanGoBack { get; }
}
