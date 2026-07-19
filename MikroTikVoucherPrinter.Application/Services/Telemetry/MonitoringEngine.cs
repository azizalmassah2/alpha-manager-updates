using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Entities.Telemetry;
using MikroTikVoucherPrinter.Domain.Interfaces.Telemetry;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Domain.Models.Telemetry;
using MikroTikVoucherPrinter.Application.Interfaces.Telemetry;

namespace MikroTikVoucherPrinter.Application.Services.Telemetry;

public class MonitoringEngine : IMonitoringEngine, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitoringEngine> _logger;
    private readonly IActiveRouterContext _activeRouterContext;
    
    private CancellationTokenSource? _engineCts;
    private bool _isRunning;
    private Task? _pollingTask;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);

    public MonitoringEngine(
        IServiceScopeFactory scopeFactory, 
        ILogger<MonitoringEngine> logger, 
        IActiveRouterContext activeRouterContext)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _activeRouterContext = activeRouterContext;
    }

    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return Task.CompletedTask;
        
        _engineCts = new CancellationTokenSource();
        _isRunning = true;
        
        _pollingTask = Task.Run(() => PollingLoopAsync(_engineCts.Token));
        
        _logger.LogInformation("Monitoring Engine started.");
        return Task.CompletedTask;
    }

    public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        _engineCts?.Cancel();
        
        if (_pollingTask != null)
        {
            try
            {
                await _pollingTask;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping polling task");
            }
        }
        
        _logger.LogInformation("Monitoring Engine stopped.");
    }

    public Task MonitorDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        // Legacy multi-device compatibility method (noop since we only monitor active router context)
        return Task.CompletedTask;
    }

    public Task StopMonitoringDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        // Legacy multi-device compatibility method (noop)
        return Task.CompletedTask;
    }

    public IEnumerable<PollingSession> GetMonitoringStatus()
    {
        // Return active router status as a polling session
        var statusList = new List<PollingSession>();
        if (_activeRouterContext.CurrentRouter != null)
        {
            statusList.Add(new PollingSession
            {
                DeviceId = _activeRouterContext.CurrentRouter.Id,
                CurrentStatus = _activeRouterContext.IsConnected ? "Connected" : "Disconnected",
                LastPollAt = DateTime.UtcNow,
                PollCount = 1
            });
        }
        return statusList;
    }

    private async Task PollingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_activeRouterContext.IsConnected && _activeRouterContext.CurrentRouter != null)
            {
                var router = _activeRouterContext.CurrentRouter;
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var executor = scope.ServiceProvider.GetRequiredService<IMikroTikCommandExecutor>();
                    var repository = scope.ServiceProvider.GetRequiredService<ITelemetryRepository>();

                    // 1. Fetch system resource telemetry
                    var resCommand = new MikroTikCommand { Command = "/system/resource/print" };
                    var resResult = await executor.ExecuteAsync(resCommand, token);
                    
                    double cpu = 0;
                    long totalMem = 0;
                    long usedMem = 0;
                    TimeSpan uptime = TimeSpan.Zero;
                    string version = string.Empty;

                    if (resResult.Success && resResult.RawData != null && resResult.RawData.Count > 0)
                    {
                        var dict = resResult.RawData.First();
                        if (dict.TryGetValue("cpu-load", out var cpuLoad) && double.TryParse(cpuLoad, out var c))
                            cpu = c;
                        if (dict.TryGetValue("total-memory", out var tmStr) && long.TryParse(tmStr, out var tm))
                            totalMem = tm;
                        if (dict.TryGetValue("free-memory", out var fmStr) && long.TryParse(fmStr, out var fm))
                            usedMem = totalMem - fm;
                        if (dict.TryGetValue("uptime", out var uptimeStr))
                            uptime = ParseUptime(uptimeStr);
                        if (dict.TryGetValue("version", out var vStr))
                            version = vStr;
                    }

                    // 2. Fetch system routerboard info (board name)
                    string boardName = "MikroTik";
                    var rbCommand = new MikroTikCommand { Command = "/system/routerboard/print" };
                    var rbResult = await executor.ExecuteAsync(rbCommand, token);
                    if (rbResult.Success && rbResult.RawData != null && rbResult.RawData.Count > 0)
                    {
                        var dict = rbResult.RawData.First();
                        if (dict.TryGetValue("board-name", out var bn))
                            boardName = bn;
                    }

                    // Store snapshot
                    var deviceSnapshot = new DeviceTelemetrySnapshot
                    {
                        Id = Guid.NewGuid(),
                        RouterId = router.Id,
                        Timestamp = DateTime.UtcNow,
                        CpuUsage = cpu,
                        MemoryUsed = usedMem,
                        MemoryTotal = totalMem,
                        Uptime = uptime,
                        HealthStatus = cpu > 90 ? DeviceHealthStatus.Critical : (cpu > 70 ? DeviceHealthStatus.Warning : DeviceHealthStatus.Healthy)
                    };

                    // 3. Fetch interfaces telemetry
                    var interfacesSnapshots = new List<InterfaceTelemetrySnapshot>();
                    var ifCommand = new MikroTikCommand { Command = "/interface/print" };
                    var ifResult = await executor.ExecuteAsync(ifCommand, token);
                    if (ifResult.Success && ifResult.RawData != null)
                    {
                        foreach (var row in ifResult.ValueOrEmptyDataList())
                        {
                            if (row.TryGetValue("name", out var name))
                            {
                                long rxBytes = 0;
                                long txBytes = 0;
                                if (row.TryGetValue("rx-byte", out var rxStr) && long.TryParse(rxStr, out var rx))
                                    rxBytes = rx;
                                if (row.TryGetValue("tx-byte", out var txStr) && long.TryParse(txStr, out var tx))
                                    txBytes = tx;

                                interfacesSnapshots.Add(new InterfaceTelemetrySnapshot
                                {
                                    Id = Guid.NewGuid(),
                                    RouterId = router.Id,
                                    InterfaceName = name,
                                    Timestamp = DateTime.UtcNow,
                                    RxBytes = rxBytes,
                                    TxBytes = txBytes,
                                    Running = row.TryGetValue("running", out var runVal) && runVal == "true"
                                });
                            }
                        }
                    }

                    // Store all snapshots to database
                    await repository.StoreSnapshotAsync(deviceSnapshot, token);
                    if (interfacesSnapshots.Count > 0)
                    {
                        await repository.StoreInterfaceSnapshotsAsync(interfacesSnapshots, token);
                    }

                    // Check for critical health alerts
                    if (deviceSnapshot.HealthStatus == DeviceHealthStatus.Critical)
                    {
                        var alert = new AlertCandidate
                        {
                            Id = Guid.NewGuid(),
                            RouterId = router.Id,
                            Timestamp = DateTime.UtcNow,
                            Severity = "Critical",
                            RuleName = "High CPU Load",
                            Message = $"CPU utilization is extremely high: {cpu}%"
                        };
                        await repository.StoreAlertCandidateAsync(alert, token);
                    }

                    // Try to dynamically update Router board details in DB if empty
                    if (string.IsNullOrEmpty(router.RouterBoard) || string.IsNullOrEmpty(router.RouterOSVersion))
                    {
                        var routerRepo = scope.ServiceProvider.GetRequiredService<IRouterRepository>();
                        var dbRouter = await routerRepo.GetByIdAsync(router.Id);
                        if (dbRouter != null)
                        {
                            dbRouter.RouterBoard = boardName;
                            dbRouter.RouterOSVersion = version;
                            dbRouter.LastSeenUtc = DateTime.UtcNow;
                            await routerRepo.UpdateAsync(dbRouter);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to poll telemetry for router {RouterId}", router.Id);
                }
            }

            try
            {
                await Task.Delay(_pollingInterval, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private TimeSpan ParseUptime(string uptime)
    {
        if (string.IsNullOrWhiteSpace(uptime)) return TimeSpan.Zero;
        
        int weeks = 0, days = 0, hours = 0, minutes = 0, seconds = 0;
        ExtractUnit(ref uptime, "w", out weeks);
        ExtractUnit(ref uptime, "d", out days);
        ExtractUnit(ref uptime, "h", out hours);
        ExtractUnit(ref uptime, "m", out minutes);
        ExtractUnit(ref uptime, "s", out seconds);

        return new TimeSpan((weeks * 7) + days, hours, minutes, seconds);
    }

    private void ExtractUnit(ref string timeStr, string unit, out int value)
    {
        value = 0;
        var idx = timeStr.IndexOf(unit);
        if (idx > -1)
        {
            int start = idx - 1;
            while (start >= 0 && char.IsDigit(timeStr[start]))
            {
                start--;
            }
            start++;
            if (int.TryParse(timeStr.Substring(start, idx - start), out var v))
            {
                value = v;
            }
            timeStr = timeStr.Remove(start, idx - start + 1);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopMonitoringAsync();
        _engineCts?.Dispose();
    }
}

public static class MikroTikResponseExtensions
{
    public static IReadOnlyList<Dictionary<string, string>> ValueOrEmptyDataList(this MikroTikResponse response)
    {
        return response?.RawData ?? new List<Dictionary<string, string>>();
    }
}
