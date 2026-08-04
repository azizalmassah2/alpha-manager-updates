using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lux.Platform.Abstractions.Interfaces;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Enums.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class ActiveRouterContext : IActiveRouterContext, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ILogger<ActiveRouterContext> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private Router? _currentRouter;
    private bool _isConnected;
    private ConnectionState _state = ConnectionState.Unknown;

    // [FIX I-04] Heartbeat: فحص دوري للاتصال
    private Timer? _heartbeatTimer;
    private CancellationTokenSource? _heartbeatCts;
    private const int HeartbeatIntervalMs = 15_000; // 15 ثانية

    public Router? CurrentRouter => _currentRouter;
    public Guid? CurrentRouterId => _currentRouter?.Id;
    public bool IsConnected => _isConnected;
    public ConnectionState State => _state;

    public event EventHandler? ActiveRouterChanged;

    public ActiveRouterContext(
        IServiceScopeFactory scopeFactory,
        ISecureStorageService secureStorageService,
        ILogger<ActiveRouterContext> logger)
    {
        _scopeFactory = scopeFactory;
        _secureStorageService = secureStorageService;
        _logger = logger;
    }

    public async Task ConnectAsync(Router router, CancellationToken cancellationToken = default)
    {
        if (router == null) throw new ArgumentNullException(nameof(router));

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionManager = scope.ServiceProvider.GetRequiredService<IMikroTikSessionManager>();

            if (_isConnected && sessionManager.IsConnected && _currentRouter?.Id == router.Id)
            {
                _logger.LogInformation("Already connected to router: {Name}", router.DisplayName);
                return; // Already connected to this router
            }

            _logger.LogInformation("Validating credentials for target router: {Name} ({Host})", router.DisplayName, router.Host);
            
            // Notify UI that a connection attempt is happening
            var previousState = _state;
            var previousRouter = _currentRouter;
            var previousIsConnected = _isConnected;

            _state = ConnectionState.Connecting;
            ActiveRouterChanged?.Invoke(this, EventArgs.Empty);

            string password = string.Empty;
            if (!string.IsNullOrEmpty(router.EncryptedPassword))
            {
                try
                {
                    password = _secureStorageService.Decrypt(router.EncryptedPassword);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt router password");
                }
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

            try
            {
                // Step 1: Attempt connection to target router
                await sessionManager.OpenSessionAsync(options, cancellationToken);
                
                // Step 2: If authentication succeeds, disconnect previous router and promote new one
                if (previousIsConnected && previousRouter != null)
                {
                    _logger.LogInformation("Closing existing connection before promoting new router.");
                    _state = ConnectionState.Switching;
                    ActiveRouterChanged?.Invoke(this, EventArgs.Empty);
                    
                    // We don't call DisconnectInternalAsync here directly to avoid triggering unnecessary UI state changes
                    // Because DisconnectInternalAsync sets state to Disconnected
                    _logger.LogInformation("Disconnecting active router session: {Name}", previousRouter.DisplayName);
                    _state = ConnectionState.Disconnected;
                    ActiveRouterChanged?.Invoke(this, EventArgs.Empty);
                }

                _currentRouter = router;
                _isConnected = true;
                _state = ConnectionState.Connected;

                // Update database connection stats
                try
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                    var dbRouter = await dbContext.Routers.FindAsync(new object[] { router.Id }, cancellationToken);
                    if (dbRouter != null)
                    {
                        dbRouter.LastConnectedUtc = DateTime.UtcNow;
                        dbRouter.LastSeenUtc = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, "Failed to update connection statistics.");
                }

                // Update settings context for API/Writing services compatibility
                try
                {
                    var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                    settingsService.Set("MikroTik.Host", router.Host);
                    settingsService.Set("MikroTik.Username", router.Username);
                    settingsService.Set("MikroTik.Password", password);
                    await settingsService.SaveAsync();
                    _logger.LogInformation("🔑 [ActiveRouterContext] Saved credentials to settings for active host: {Host}", router.Host);
                }
                catch (Exception settingsEx)
                {
                    _logger.LogWarning(settingsEx, "Failed to update global settings with active router credentials.");
                }

                _logger.LogInformation("Successfully connected to router: {Name}", router.DisplayName);
                System.Console.WriteLine($"[CONTEXT] Successfully connected to router: {router.DisplayName}. State: {_state}, IsConnected: {_isConnected}");
                ActiveRouterChanged?.Invoke(this, EventArgs.Empty);

                // إعادة تهيئة Circuit Breaker لضمان إمكانية المزامنة فور عودة الاتصال
                try
                {
                    var integrationService = scope.ServiceProvider.GetService<MikroTikVoucherPrinter.Application.Interfaces.IMikroTikIntegrationService>();
                    integrationService?.ResetCircuitBreaker();
                    _logger.LogInformation("✅ [ActiveRouterContext] Circuit Breaker reset after successful reconnection.");
                }
                catch (Exception cbEx)
                {
                    _logger.LogWarning(cbEx, "Failed to reset circuit breaker.");
                }

                // [FIX I-04] بدء Heartbeat وإيقاف مؤقت إعادة الاتصال بعد الاتصال الناجح
                StopReconnectLoop();
                StartHeartbeat();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to router: {Name}", router.DisplayName);
                
                // Step 3: If authentication fails, keep existing router active
                _currentRouter = previousRouter;
                _isConnected = previousIsConnected;
                
                if (ex.Message.Contains("authenticate", StringComparison.OrdinalIgnoreCase) || 
                    ex.Message.Contains("login", StringComparison.OrdinalIgnoreCase))
                {
                    _state = ConnectionState.AuthenticationFailed;
                }
                else if (ex is OperationCanceledException || ex is TimeoutException)
                {
                    _state = ConnectionState.Timeout;
                }
                else
                {
                    _state = ConnectionState.Error;
                }
                ActiveRouterChanged?.Invoke(this, EventArgs.Empty);
                throw;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            await DisconnectInternalAsync(cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task DisconnectInternalAsync(CancellationToken cancellationToken)
    {
        if (_currentRouter == null) return;

        // [FIX I-04] إيقاف Heartbeat عند قطع الاتصال
        StopHeartbeat();

        _logger.LogInformation("Disconnecting active router session: {Name}", _currentRouter.DisplayName);

        using var scope = _scopeFactory.CreateScope();
        var sessionManager = scope.ServiceProvider.GetRequiredService<IMikroTikSessionManager>();

        try
        {
            await sessionManager.CloseSessionAsync(cancellationToken);
            
            bool wasConnected = _isConnected || _currentRouter != null;
            _isConnected = false;
            _currentRouter = null;
            _state = ConnectionState.Disconnected;
            
            if (wasConnected)
            {
                ActiveRouterChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect from router: {Name}. State remains unchanged.", _currentRouter.DisplayName);
            _state = ConnectionState.Error;
            ActiveRouterChanged?.Invoke(this, EventArgs.Empty);
            throw; // Re-throw to inform caller that disconnect failed
        }
    }

    public async Task SwitchRouterAsync(Router router, CancellationToken cancellationToken = default)
    {
        // ConnectAsync now handles disconnecting the previous router safely
        await ConnectAsync(router, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    //  [FIX I-04] Heartbeat — فحص دوري للاتصال
    // ─────────────────────────────────────────────────────────────
    private void StartHeartbeat()
    {
        StopHeartbeat(); // إيقاف أي Heartbeat سابق
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTimer = new Timer(
            async _ => await CheckConnectionAliveAsync(),
            null,
            HeartbeatIntervalMs,
            HeartbeatIntervalMs);
        _logger.LogDebug("[Heartbeat] بدأ فحص الاتصال كل {Interval}ms", HeartbeatIntervalMs);
    }

    private void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    // [FIX-RECONNECT] Reconnect polling timer
    private Timer? _reconnectTimer;
    private bool _isReconnecting;

    public void MarkDisconnected(string reason)
    {
        if (!_isConnected && _state == ConnectionState.Disconnected) return;
        
        var routerName = _currentRouter?.DisplayName ?? "الراوتر";
        _logger.LogWarning("Marking router {Name} as disconnected. Reason: {Reason}", routerName, reason);
        
        StopHeartbeat();
        _isConnected = false;
        _state = ConnectionState.Disconnected;
        ActiveRouterChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var eventBus = scope.ServiceProvider.GetService<IEventBus>();
            eventBus?.Publish(new MikroTikVoucherPrinter.Application.Events.AlertGeneratedEvent(
                new Lux.Platform.Abstractions.Models.Monitoring.Alert
                {
                    Id = Guid.NewGuid(),
                    Message = $"🔴 انقطع الاتصال بالراوتر ({routerName})! يرجى الفحص والتحقق من كابل الشبكة.",
                    Severity = Lux.Platform.Abstractions.Models.Monitoring.AlertSeverity.Critical,
                    Timestamp = DateTime.UtcNow
                }));
        }
        catch { }

        StartReconnectLoop();
    }

    private void StartReconnectLoop()
    {
        StopReconnectLoop();
        _reconnectTimer = new Timer(async _ => await TryAutoReconnectAsync(), null, 4000, 4000);
    }

    private void StopReconnectLoop()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
        _isReconnecting = false;
    }

    private async Task TryAutoReconnectAsync()
    {
        if (_isConnected || _currentRouter == null || _isReconnecting) return;

        _isReconnecting = true;
        try
        {
            var targetRouter = _currentRouter;
            _logger.LogInformation("🔄 [AutoReconnect] محاولة إعادة الاتصال التلقائي بالراوتر: {Name}", targetRouter.DisplayName);
            await ConnectAsync(targetRouter);

            if (_isConnected)
            {
                StopReconnectLoop();
                _logger.LogInformation("🎉 [AutoReconnect] نجحت إعادة الاتصال التلقائي بالراوتر: {Name}", targetRouter.DisplayName);
                
                using var scope = _scopeFactory.CreateScope();
                var eventBus = scope.ServiceProvider.GetService<IEventBus>();
                eventBus?.Publish(new MikroTikVoucherPrinter.Application.Events.AlertGeneratedEvent(
                    new Lux.Platform.Abstractions.Models.Monitoring.Alert
                    {
                        Id = Guid.NewGuid(),
                        Message = $"🟢 تم استعادة الاتصال بالراوتر ({targetRouter.DisplayName}) تلقائياً!",
                        Severity = Lux.Platform.Abstractions.Models.Monitoring.AlertSeverity.Information,
                        Timestamp = DateTime.UtcNow
                    }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[AutoReconnect] لا تزال محاولة الاتصال غير متاحة: {Message}", ex.Message);
        }
        finally
        {
            _isReconnecting = false;
        }
    }

    private async Task CheckConnectionAliveAsync()
    {
        if (!_isConnected || _currentRouter == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionManager = scope.ServiceProvider.GetRequiredService<IMikroTikSessionManager>();

            if (!sessionManager.IsConnected)
            {
                MarkDisconnected("انقطع اتصال مقبس الراوتر");
                return;
            }

            var commandExecutor = scope.ServiceProvider.GetRequiredService<IMikroTikCommandExecutor>();
            var pingCmd = new MikroTikCommand { Command = "/system/identity/print" };
            var res = await commandExecutor.ExecuteAsync(pingCmd, _heartbeatCts?.Token ?? CancellationToken.None);
            if (!res.Success)
            {
                MarkDisconnected("فشل استجابة فحص هُوية الراوتر");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Heartbeat] فشل فحص الاتصال");
            MarkDisconnected($"انقطاع كابل الشبكة أو الاتصال: {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopHeartbeat();
        StopReconnectLoop();
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
