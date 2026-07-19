using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MikroTikVoucherPrinter.UI.ViewModels;

/// <summary>
/// ViewModel الأساسي - يدعم عمليات التحميل الداعمة للـ Cancellation مع رسائل الحالات
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    protected readonly ILogger Logger;
    private CancellationTokenSource? _cancellationTokenSource;

    protected BaseViewModel(ILogger logger)
    {
        Logger = logger;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// هل يمكن إلغاء العملية الحالية؟
    /// </summary>
    public bool CanCancel => IsBusy && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;

    /// <summary>
    /// أمر إلغاء العملية الجارية
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    protected virtual void Cancel()
    {
        if (CanCancel)
        {
            StatusMessage = "جاري الإلغاء...";
            _cancellationTokenSource?.Cancel();
            Logger.LogInformation("تم طلب إلغاء العملية من قِبل المستخدم في {ViewModel}", GetType().Name);
        }
    }

    public virtual Task InitializeAsync(object? parameter = null)
    {
        return Task.CompletedTask;
    }

    public virtual Task CleanupAsync()
    {
        // إلغاء أي مهام متأخرة عند مغادرة الصفحة
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// تنفيذ عملية خلفية تدعم الإلغاء والتعامل الذكي مع الأخطاء
    /// </summary>
    protected async Task ExecuteBusyAsync(Func<CancellationToken, Task> operation, string? loadingMessage = null)
    {
        if (IsBusy) return;

        _cancellationTokenSource = new CancellationTokenSource();
        CancelCommand.NotifyCanExecuteChanged();

        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = loadingMessage ?? "جاري التحميل...";

            await operation(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("تم إلغاء المهمة بنجاح في {ViewModel}", GetType().Name);
            StatusMessage = "تم الإلغاء بناءً على طلبك";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "خطأ في {ViewModelName}", GetType().Name);
        }
        finally
        {
            IsBusy = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            CancelCommand.NotifyCanExecuteChanged();
            
            // احتفظ برسالة الإلغاء لفترة قصيرة قبل المسح إن وجدت
            if (StatusMessage == (loadingMessage ?? "جاري التحميل...")) 
                StatusMessage = string.Empty;
        }
    }

    /// <summary>
    /// تنفيذ عملية خلفية تدعم الإلغاء وترجع قيمة
    /// </summary>
    protected async Task<T?> ExecuteBusyAsync<T>(Func<CancellationToken, Task<T>> operation, string? loadingMessage = null)
    {
        if (IsBusy) return default;

        _cancellationTokenSource = new CancellationTokenSource();
        CancelCommand.NotifyCanExecuteChanged();

        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = loadingMessage ?? "جاري التحميل...";

            return await operation(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("تم إلغاء المهمة بنجاح في {ViewModel}", GetType().Name);
            StatusMessage = "تم الإلغاء بناءً على طلبك";
            return default;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "خطأ في {ViewModelName}", GetType().Name);
            return default;
        }
        finally
        {
            IsBusy = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            CancelCommand.NotifyCanExecuteChanged();

            if (StatusMessage == (loadingMessage ?? "جاري التحميل...")) 
                StatusMessage = string.Empty;
        }
    }
}
