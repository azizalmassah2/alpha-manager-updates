using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lux.Management.Console.Core;

public interface IBusyIndicatorService : INotifyPropertyChanged
{
    bool IsBusy { get; }
    string? BusyMessage { get; }

    void Show(string? message = null);
    void Hide();
}

public class BusyIndicatorService : IBusyIndicatorService
{
    private bool _isBusy;
    private string? _busyMessage;
    private int _busyCount;
    private readonly object _lock = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    public string? BusyMessage
    {
        get => _busyMessage;
        private set
        {
            if (_busyMessage != value)
            {
                _busyMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Show(string? message = null)
    {
        lock (_lock)
        {
            _busyCount++;
            IsBusy = true;
            if (message != null)
            {
                BusyMessage = message;
            }
        }
    }

    public void Hide()
    {
        lock (_lock)
        {
            if (_busyCount > 0)
            {
                _busyCount--;
            }

            if (_busyCount == 0)
            {
                IsBusy = false;
                BusyMessage = null;
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
