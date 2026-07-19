using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;

namespace Lux.Management.Console.Navigation;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IShellState _shellState;
    private readonly Stack<ViewModelBase> _history = new();

    public NavigationService(IServiceProvider serviceProvider, IShellState shellState)
    {
        _serviceProvider = serviceProvider;
        _shellState = shellState;
    }

    public bool CanGoBack => _history.Count > 0;

    public void Navigate<TViewModel>() where TViewModel : ViewModelBase
    {
        if (_shellState.CurrentViewModel is ViewModelBase currentVm)
        {
            _history.Push(currentVm);
        }

        var vm = _serviceProvider.GetRequiredService<TViewModel>();
        _shellState.CurrentViewModel = vm;
    }

    public void NavigateToDevice(Guid deviceId)
    {
        // Device detail navigation removed — device management is now
        // handled through the MikroTik Center and Modems sections directly.
        throw new NotImplementedException("Direct device navigation is deprecated. Use module-level navigation instead.");
    }

    public void NavigateToProject(Guid projectId)
    {
        throw new NotImplementedException();
    }

    public void NavigateToOperation(Guid operationId)
    {
        throw new NotImplementedException();
    }

    public void NavigateToAlert(Guid alertId)
    {
        throw new NotImplementedException();
    }

    public void GoBack()
    {
        if (CanGoBack)
        {
            _shellState.CurrentViewModel = _history.Pop();
        }
    }
}
