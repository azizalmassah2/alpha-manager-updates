using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lux.Management.Console.Core;

public interface IShellState : INotifyPropertyChanged
{
    object? CurrentViewModel { get; set; }
    bool IsRegistered { get; set; }
    void NotifyCurrentViewModelChanged();
}

public class ShellState : IShellState
{
    private object? _currentViewModel;
    private bool _isRegistered = true; // الافتراضي صالح، وسيتم تحديثه عند التحقق

    public void NotifyCurrentViewModelChanged()
    {
        OnPropertyChanged(nameof(CurrentViewModel));
    }

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        set
        {
            if (_currentViewModel != value)
            {
                _currentViewModel = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRegistered
    {
        get => _isRegistered;
        set
        {
            if (_isRegistered != value)
            {
                _isRegistered = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
