using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lux.Management.Console.Core;

public interface ISelectionContext : INotifyPropertyChanged
{
    Guid? ActiveProjectId { get; set; }
    Guid? ActiveDeviceId { get; set; }
}

public class SelectionContext : ISelectionContext
{
    private Guid? _activeProjectId;
    private Guid? _activeDeviceId;

    public Guid? ActiveProjectId
    {
        get => _activeProjectId;
        set
        {
            if (_activeProjectId != value)
            {
                _activeProjectId = value;
                OnPropertyChanged();
            }
        }
    }

    public Guid? ActiveDeviceId
    {
        get => _activeDeviceId;
        set
        {
            if (_activeDeviceId != value)
            {
                _activeDeviceId = value;
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
