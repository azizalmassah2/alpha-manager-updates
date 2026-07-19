using System;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.UI.Services;

/// <summary>
/// تطبيق واجهة حالة التنقل. 
/// مفيد في ربط الواجهة (MainViewModel) بحالة التطبيق بشكل لا يعتمد على NavigationService للـ Data State.
/// </summary>
public class NavigationState : INavigationState
{
    private Type? _currentViewModel;

    public Type? CurrentViewModel
    {
        get => _currentViewModel;
        set
        {
            _currentViewModel = value;
            StateChanged?.Invoke();
        }
    }

    public event Action? StateChanged;
}
