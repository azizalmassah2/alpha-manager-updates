using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.State;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.Services;

public class AutoRefreshService : IAutoRefreshService, IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IDeviceStateManager _deviceStateManager;
    private readonly IVoucherBackgroundImportManager _backgroundSweepManager;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    private int _currentSegmentIndex = 0;
    
    private readonly Dictionary<Guid, DateTime> _lastDownloadTimes = new();
    
    public int IntervalSeconds { get; set; } = 30;
    public bool IsRunning => _refreshTask != null && !_refreshTask.IsCompleted;

    private readonly List<Func<Task>> _callbacks = new();
    private readonly object _callbacksLock = new();

    public AutoRefreshService(
        IEventBus eventBus, 
        IDeviceStateManager deviceStateManager,
        IVoucherBackgroundImportManager backgroundSweepManager,
        IActiveRouterContext activeRouterContext,
        ISettingsService settingsService)
    {
        _eventBus = eventBus;
        _deviceStateManager = deviceStateManager;
        _backgroundSweepManager = backgroundSweepManager;
        _activeRouterContext = activeRouterContext;
        _settingsService = settingsService;

        // مسح كاش الـ Sweep وإعادة ترتيب الشرائح عند تغيير الراوتر النشط
        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        // مسح الكاش حتى تُجلب بيانات الراوتر الجديد من الصفر في أول دورة
        var currentId = _activeRouterContext.CurrentRouterId;
        if (currentId.HasValue && currentId.Value != Guid.Empty)
            _backgroundSweepManager.InvalidateSweepCache(currentId.Value);

        _currentSegmentIndex = 0;  // إعادة البدء من الشريحة الأولى
    }

    public void RegisterCallback(Func<Task> callback)
    {
        lock (_callbacksLock)
        {
            if (!_callbacks.Contains(callback))
            {
                _callbacks.Add(callback);
            }
        }
    }

    public void UnregisterCallback(Func<Task> callback)
    {
        lock (_callbacksLock)
        {
            _callbacks.Remove(callback);
        }
    }

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _refreshTask = Task.Run(() => RefreshLoop(_cts.Token));
    }

    public void Stop()
    {
        if (_cts == null) return;
        _cts.Cancel();
        _refreshTask?.Wait();
        _cts.Dispose();
        _cts = null;
    }

    private async Task RefreshLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await _deviceStateManager.RefreshAllDevicesAsync();
                
                var currentRouterId = _activeRouterContext.CurrentRouterId;
                if (currentRouterId.HasValue && currentRouterId.Value != Guid.Empty)
                {
                    try
                    {
                        await _backgroundSweepManager.RunSegmentedSweepAsync(currentRouterId.Value, _currentSegmentIndex, 36, token);
                        _currentSegmentIndex = (_currentSegmentIndex + 1) % 36;
                    }
                    catch
                    {
                        // Ignore background sweep failures to keep the refresh loop alive
                    }

                    // Periodic UserManager DB download (caching)
                    try
                    {
                        var intervalMinutes = _settingsService.Get("UserManagerImportInterval", 5);
                        if (intervalMinutes < 1) intervalMinutes = 1;

                        if (ShouldDownloadDatabase(currentRouterId.Value, intervalMinutes))
                        {
                            await _backgroundSweepManager.DownloadAndCacheDbAsync(currentRouterId.Value, token);
                            _lastDownloadTimes[currentRouterId.Value] = DateTime.UtcNow;
                        }
                    }
                    catch
                    {
                        // Ignore db download background errors
                    }
                }

                List<Func<Task>> currentCallbacks;
                lock (_callbacksLock)
                {
                    currentCallbacks = _callbacks.ToList();
                }

                foreach (var callback in currentCallbacks)
                {
                    try
                    {
                        await callback();
                    }
                    catch
                    {
                        // Ignore individual callback failures
                    }
                }

                _eventBus.Publish(new AutoRefreshTriggeredEvent());
            }
            catch (Exception)
            {
                // In a real app we would log this
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private bool ShouldDownloadDatabase(Guid routerId, int intervalMinutes)
    {
        if (!_lastDownloadTimes.TryGetValue(routerId, out var lastTime))
        {
            return true;
        }
        return (DateTime.UtcNow - lastTime).TotalMinutes >= intervalMinutes;
    }

    public void Dispose()
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        Stop();
    }
}

public class AutoRefreshTriggeredEvent { }
