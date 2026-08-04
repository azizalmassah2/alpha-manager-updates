using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

public class RouterHealthService : IRouterHealthService, IHostedService, IDisposable
{
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISecureStorageService _secureStorage;
    private readonly ILogger<RouterHealthService> _logger;

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RouterHealthStatus CurrentStatus { get; private set; } = new();
    public event EventHandler<RouterHealthStatus>? HealthUpdated;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(3); // Configurable

    public RouterHealthService(
        IActiveRouterContext activeRouterContext,
        IServiceScopeFactory scopeFactory,
        ISecureStorageService secureStorage,
        ILogger<RouterHealthService> logger)
    {
        _activeRouterContext = activeRouterContext;
        _scopeFactory = scopeFactory;
        _secureStorage = secureStorage;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _activeRouterContext.ActiveRouterChanged += OnActiveRouterChanged;
        // Check initial state
        if (_activeRouterContext.IsConnected)
        {
            StartPolling();
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _activeRouterContext.ActiveRouterChanged -= OnActiveRouterChanged;
        StopPolling();
        return Task.CompletedTask;
    }

    private void OnActiveRouterChanged(object? sender, EventArgs e)
    {
        if (_activeRouterContext.IsConnected)
        {
            StartPolling();
        }
        else
        {
            StopPolling();
            UpdateStatus(new RouterHealthStatus { IsConnected = false, OverallHealth = RouterHealthLevel.Unknown });
        }
    }

    private void StartPolling()
    {
        _lock.Wait();
        try
        {
            if (_pollingCts != null) return; // Already polling

            _pollingCts = new CancellationTokenSource();
            _pollingTask = Task.Run(() => PollingLoopAsync(_pollingCts.Token), _pollingCts.Token);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void StopPolling()
    {
        _lock.Wait();
        try
        {
            if (_pollingCts != null)
            {
                _pollingCts.Cancel();
                _pollingCts.Dispose();
                _pollingCts = null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task PollingLoopAsync(CancellationToken cancellationToken)
    {
        var router = _activeRouterContext.CurrentRouter;
        if (router == null) return;

        string password = string.Empty;
        if (!string.IsNullOrEmpty(router.EncryptedPassword))
        {
            password = _secureStorage.Decrypt(router.EncryptedPassword);
        }

        var options = new MikroTikConnectionOptions
        {
            Host = router.Host,
            Port = router.Port,
            Username = router.Username,
            Password = password,
            UseSsl = false,
            ProviderType = RouterOsProviderType.Api
        };

        // We use a dedicated scope to get the command executor
        using var scope = _scopeFactory.CreateScope();
        var commandExecutor = scope.ServiceProvider.GetRequiredService<IMikroTikCommandExecutor>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var status = await FetchHealthStatusAsync(commandExecutor, cancellationToken);
                    UpdateStatus(status);
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException) break;
                    
                    _logger.LogWarning(ex, "Failed to fetch router health status");
                    UpdateStatus(new RouterHealthStatus 
                    { 
                        IsConnected = false, 
                        OverallHealth = RouterHealthLevel.Unknown 
                    });
                    
                    _activeRouterContext.MarkDisconnected("انقطع الاتصال أو كابل الشبكة أثناء مراقبة النظام");
                    break; 
                }

                await Task.Delay(PollingInterval, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Background health polling session failed.");
            }
        }
    }

    private async Task<RouterHealthStatus> FetchHealthStatusAsync(IMikroTikCommandExecutor executor, CancellationToken cancellationToken)
    {
        var status = new RouterHealthStatus { IsConnected = true, LastUpdated = DateTime.Now };

        // 1. Fetch System Resources
        var resourceCmd = new MikroTikCommand { Command = "/system/resource/print" };
        var resourceRes = await executor.ExecuteAsync(resourceCmd, cancellationToken);
        var resourceData = resourceRes.RawData.FirstOrDefault();

        if (resourceData != null)
        {
            if (resourceData.TryGetValue("cpu-load", out var cpuStr) && double.TryParse(cpuStr, out var cpu))
                status.CpuLoadPercent = cpu;

            if (resourceData.TryGetValue("free-memory", out var freeMemStr) && double.TryParse(freeMemStr, out var freeMem) &&
                resourceData.TryGetValue("total-memory", out var totMemStr) && double.TryParse(totMemStr, out var totMem) && totMem > 0)
            {
                status.MemoryUsagePercent = Math.Round(((totMem - freeMem) / totMem) * 100.0, 1);
            }

            if (resourceData.TryGetValue("free-hdd-space", out var freeHddStr) && double.TryParse(freeHddStr, out var freeHdd) &&
                resourceData.TryGetValue("total-hdd-space", out var totHddStr) && double.TryParse(totHddStr, out var totHdd) && totHdd > 0)
            {
                status.DiskUsagePercent = Math.Round(((totHdd - freeHdd) / totHdd) * 100.0, 1);
            }

            if (resourceData.TryGetValue("uptime", out var uptime)) status.Uptime = uptime;
            if (resourceData.TryGetValue("version", out var version)) status.Version = version;
            if (resourceData.TryGetValue("board-name", out var board)) status.BoardName = board;
        }

        // 2. Try Fetch Health (Temperature/Voltage) - Might not exist on all models
        try
        {
            var healthCmd = new MikroTikCommand { Command = "/system/health/print" };
            var response = await executor.ExecuteAsync(healthCmd, cancellationToken);
            var healthData = response.RawData.FirstOrDefault();
            if (healthData != null)
            {
                if (healthData.TryGetValue("temperature", out var tempStr) && double.TryParse(tempStr, out var temp))
                    status.Temperature = temp;
                if (healthData.TryGetValue("voltage", out var voltStr) && double.TryParse(voltStr, out var volt))
                    status.Voltage = volt;
            }
        }
        catch
        {
            // Ignore if /system/health doesn't exist on this hardware
        }

        // Calculate Overall Health
        status.OverallHealth = CalculateHealthLevel(status);

        return status;
    }

    private RouterHealthLevel CalculateHealthLevel(RouterHealthStatus status)
    {
        if (status.CpuLoadPercent >= 95 || status.MemoryUsagePercent >= 95 || status.DiskUsagePercent >= 95)
            return RouterHealthLevel.Critical;

        if (status.CpuLoadPercent >= 80 || status.MemoryUsagePercent >= 85 || status.DiskUsagePercent >= 85 || (status.Temperature.HasValue && status.Temperature > 65))
            return RouterHealthLevel.Warning;

        return RouterHealthLevel.Healthy;
    }

    private void UpdateStatus(RouterHealthStatus newStatus)
    {
        CurrentStatus = newStatus;
        HealthUpdated?.Invoke(this, newStatus);
    }

    public void Dispose()
    {
        StopPolling();
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}



