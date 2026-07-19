using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.UI.ViewModels;

namespace MikroTikVoucherPrinter.UI.Services;

/// <summary>
/// خدمة التنقل - تدير التنقل بين الصفحات باستخدام DI
/// تعتمد على INavigationState لفصل الحالة عن منطق التنقل
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationState _navigationState;
    private readonly ILogger<NavigationService> _logger;
    private readonly Dictionary<string, Type> _pageRegistry = new();
    private readonly Stack<string> _navigationStack = new();
    
    private string _currentPage = string.Empty;
    public string CurrentPage => _currentPage;
    public bool CanGoBack => _navigationStack.Count > 1;

    public event Action<string>? PageChanged;

    /// <summary>
    /// حدث تغيير ViewModel - يُستدعى بعد تهيئة ViewModel للصفحة المطلوبة
    /// </summary>
    public event Action<BaseViewModel>? ViewModelChanged;

    public NavigationService(
        IServiceProvider serviceProvider,
        INavigationState navigationState,
        ILogger<NavigationService> logger)
    {
        _serviceProvider = serviceProvider;
        _navigationState = navigationState;
        _logger = logger;
    }

    /// <summary>
    /// تسجيل صفحة جديدة في خريطة التنقل
    /// </summary>
    public void RegisterPage<TViewModel>(string pageKey) where TViewModel : BaseViewModel
    {
        _pageRegistry[pageKey] = typeof(TViewModel);
        _logger.LogDebug("تم تسجيل الصفحة: {PageKey} -> {ViewModelType}", pageKey, typeof(TViewModel).Name);
    }

    public void NavigateTo(string pageKey)
    {
        NavigateTo(pageKey, null!);
    }

    public void NavigateTo(string pageKey, object parameter)
    {
        if (!_pageRegistry.TryGetValue(pageKey, out var viewModelType))
        {
            _logger.LogWarning("لم يتم العثور على الصفحة (Deep Linking unsupported or bad key): {PageKey}", pageKey);
            return;
        }

        try
        {
            // تحديث State المفصولة أولاً
            _navigationState.CurrentViewModel = viewModelType;

            var viewModel = (BaseViewModel)_serviceProvider.GetRequiredService(viewModelType);

            // حفظ المسار في Stack لتمكين العودة
            if (_currentPage != pageKey)
            {
                _navigationStack.Push(pageKey);
            }

            _currentPage = pageKey;

            // تهيئة الصفحة الجديدة
            _ = viewModel.InitializeAsync(parameter);

            ViewModelChanged?.Invoke(viewModel);
            PageChanged?.Invoke(pageKey);

            _logger.LogInformation("التنقل إلى {PageKey} بنجاح", pageKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل التنقل إلى {PageKey} لأن التهيئة فشلت", pageKey);
        }
    }

    public void GoBack()
    {
        if (_navigationStack.Count <= 1) return;

        _navigationStack.Pop(); // إزالة الصفحة الحالية
        var previousPage = _navigationStack.Peek();
        _currentPage = string.Empty; // إعادة تعيين لتجنب منع التنقل مرة أخرى لنفس الصفحة
        NavigateTo(previousPage);
    }
}
