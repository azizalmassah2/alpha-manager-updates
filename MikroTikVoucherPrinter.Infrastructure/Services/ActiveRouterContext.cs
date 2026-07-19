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

    public void Dispose()
    {
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
