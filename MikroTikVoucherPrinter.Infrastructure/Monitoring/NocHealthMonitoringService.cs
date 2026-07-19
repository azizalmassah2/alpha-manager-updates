using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Infrastructure.Data;
using Lux.Platform.Abstractions.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Monitoring
{
    public class NocHealthMonitoringService : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IActiveRouterContext _activeRouterContext;
        private readonly ISettingsService _settingsService;
        private readonly IEventBus _eventBus;
        private readonly IEnumerable<IHealthMonitorProvider> _providers;
        private readonly ILogger<NocHealthMonitoringService> _logger;

        private CancellationTokenSource? _cts;
        private Task? _executingTask;

        // Track consecutive failures per RouterId and VlanId
        private readonly ConcurrentDictionary<(Guid RouterId, string VlanId), int> _consecutiveFailures = new();
        
        // Track last seen timestamps per RouterId and VlanId
        private readonly ConcurrentDictionary<(Guid RouterId, string VlanId), DateTime> _lastSeenTimestamps = new();

        // Track last known statuses to publish only on actual change
        private readonly ConcurrentDictionary<(Guid RouterId, string VlanId), (string Status, double Latency)> _lastStates = new();

        public NocHealthMonitoringService(
            IServiceScopeFactory scopeFactory,
            IActiveRouterContext activeRouterContext,
            ISettingsService settingsService,
            IEventBus eventBus,
            IEnumerable<IHealthMonitorProvider> providers,
            ILogger<NocHealthMonitoringService> logger)
        {
            _scopeFactory = scopeFactory;
            _activeRouterContext = activeRouterContext;
            _settingsService = settingsService;
            _eventBus = eventBus;
            _providers = providers;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🚀 NOC Health Monitoring Service is starting...");
            _cts = new CancellationTokenSource();
            _executingTask = ExecuteMonitoringLoopAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 NOC Health Monitoring Service is stopping...");
            if (_cts == null) return;

            try
            {
                _cts.Cancel();
            }
            finally
            {
                if (_executingTask != null)
                {
                    await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
                }
            }
        }

        private async Task ExecuteMonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var interval = _settingsService.Get<int>("NocMonitoringInterval", 100);
                    if (interval < 1) interval = 100;

                    await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

                    if (!_activeRouterContext.IsConnected || _activeRouterContext.CurrentRouter == null)
                    {
                        continue;
                    }

                    var currentRouterId = _activeRouterContext.CurrentRouter.Id;

                    // Fetch active monitoring configurations for the connected router
                    List<Domain.Entities.Platform.VlanMonitoringConfig> configs;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                        configs = await db.VlanMonitoringConfigs
                            .Where(c => c.RouterId == currentRouterId && c.EnableMonitoring)
                            .ToListAsync(cancellationToken);
                    }

                    if (configs == null || configs.Count == 0)
                    {
                        continue;
                    }

                    var pingTimeout = TimeSpan.FromMilliseconds(_settingsService.Get<int>("NocPingTimeout", 2000));
                    var warningThresholdMs = _settingsService.Get<int>("NocWarningThresholdMs", 150);

                    // Execute checks concurrently (up to 20 checks in parallel)
                    var options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 20,
                        CancellationToken = cancellationToken
                    };

                    var provider = _providers.FirstOrDefault(p => p.MonitorType == "Ping");
                    if (provider == null)
                    {
                        _logger.LogError("Ping health monitor provider not registered.");
                        continue;
                    }

                    await Parallel.ForEachAsync(configs, options, async (config, ct) =>
                    {
                        var key = (config.RouterId, config.VlanId);
                        var result = await provider.CheckHealthAsync(config.DeviceIp, pingTimeout, ct);

                        string status;
                        double latency = 0;
                        DateTime? lastSeen = null;

                        if (result.IsSuccess)
                        {
                            _consecutiveFailures[key] = 0;
                            var now = DateTime.Now;
                            _lastSeenTimestamps[key] = now;
                            lastSeen = now;

                            latency = result.LatencyMs;
                            status = latency > warningThresholdMs ? "Warning" : "Healthy";
                        }
                        else
                        {
                            _consecutiveFailures.AddOrUpdate(key, 1, (_, count) => count + 1);
                            var failures = _consecutiveFailures[key];

                            if (failures >= 3)
                            {
                                status = "Offline";
                            }
                            else
                            {
                                status = "Warning"; // Warn during transient failure states
                            }

                            if (_lastSeenTimestamps.TryGetValue(key, out var seenTime))
                            {
                                lastSeen = seenTime;
                            }
                        }

                        // Check if state changed significantly to publish
                        var changed = true;
                        if (_lastStates.TryGetValue(key, out var lastState))
                        {
                            if (lastState.Status == status && Math.Abs(lastState.Latency - latency) < 5)
                            {
                                changed = false; // status is same and latency difference is minor
                            }
                        }

                        if (changed)
                        {
                            _lastStates[key] = (status, latency);
                            _eventBus.Publish(new VlanHealthChangedEvent
                            {
                                RouterId = config.RouterId,
                                VlanId = config.VlanId,
                                DeviceIp = config.DeviceIp,
                                Status = status,
                                LatencyMs = latency,
                                LastSeen = lastSeen
                            });
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // Swallowing cancellation to exit loop cleanly
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in NOC health monitoring cycle.");
                }
            }
        }

        public void Dispose()
        {
            _cts?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
