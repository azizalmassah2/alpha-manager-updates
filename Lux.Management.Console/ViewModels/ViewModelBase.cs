using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.ViewModels;

public abstract partial class ViewModelBase : ObservableObject, IDisposable
{
    protected readonly IPermissionService _permissionService;
    protected readonly IEventBus _eventBus;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _statusMessage;

    partial void OnIsBusyChanged(bool value)
    {
        if (System.Windows.Application.Current is App app && app.ServiceProvider != null)
        {
            var busyService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IBusyIndicatorService>(app.ServiceProvider);
            if (busyService != null)
            {
                if (value) busyService.Show(BusyMessage);
                else busyService.Hide();
            }
        }
    }

    protected ViewModelBase(IPermissionService permissionService, IEventBus eventBus)
    {
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    protected async Task ExecuteBusyAsync(Func<Task> action, string busyMessage = "Processing...")
    {
        await ExecuteBusyAsync(async _ => await action(), busyMessage);
    }

    protected async Task ExecuteBusyAsync(Func<System.Threading.CancellationToken, Task> action, string busyMessage = "Processing...")
    {
        if (IsBusy) return;

        IsBusy = true;
        BusyMessage = busyMessage;
        _lastBusyError = null;

        try
        {
            await action(System.Threading.CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            _lastBusyError = "تم إلغاء العملية.";
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] Operation cancelled.");
        }
        catch (Exception ex)
        {
            _lastBusyError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] Error: {ex}");
            // إعادة رمي الاستثناء حتى يتمكن المستدعي من معالجته
            throw;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    // آخر خطأ من ExecuteBusyAsync (للاستخدام في الـ ViewModels الفرعية)
    protected string? _lastBusyError;

    protected void FireAndForget(Func<Task> taskFactory, string? operationName = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await taskFactory();
            }
            catch (OperationCanceledException) { /* متوقع */ }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [FireAndForget] {operationName ?? "unknown"} failed: {ex}");
            }
        });
    }

    public virtual void Dispose()
    {
        // Override in subclasses to release resources / unsubscribe events
    }
}
