using System;
using System.Threading.Tasks;
using System.Windows;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.Services;

/// <summary>
/// WPF implementation of IDispatcherService.
/// </summary>
public class DispatcherService : IDispatcherService
{
    public void Invoke(Action action)
    {
        if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }

    public Task InvokeAsync(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return Task.CompletedTask;
        }
        
        return Application.Current.Dispatcher.InvokeAsync(action).Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        if (Application.Current?.Dispatcher == null)
        {
            return Task.FromResult(function());
        }
        
        return Application.Current.Dispatcher.InvokeAsync(function).Task;
    }
}
