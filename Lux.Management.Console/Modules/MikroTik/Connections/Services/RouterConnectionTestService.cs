using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Services;

public class RouterConnectionTestService : IConnectionTestService
{
    private readonly IMikroTikSessionManager _sessionManager;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ILogger<RouterConnectionTestService> _logger;

    public RouterConnectionTestService(
        IMikroTikSessionManager sessionManager,
        ISecureStorageService secureStorageService,
        ILogger<RouterConnectionTestService> logger)
    {
        _sessionManager = sessionManager;
        _secureStorageService = secureStorageService;
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(Router router, CancellationToken cancellationToken = default)
    {
        var result = new ConnectionTestResult();
        var sw = Stopwatch.StartNew();

        try
        {
            string password = string.Empty;
            if (!string.IsNullOrEmpty(router.EncryptedPassword))
            {
                try
                {
                    password = _secureStorageService.Decrypt(router.EncryptedPassword);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt router password during test");
                    return new ConnectionTestResult
                    {
                        Success = false,
                        Reason = "فشل في فك تشفير كلمة المرور"
                    };
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

            // Test Open Session
            await _sessionManager.OpenSessionAsync(options, cancellationToken);
            
            // Try fetch identity and version if we have an API client
            // For now, we just prove login success
            // In a real scenario, IMikroTikSessionManager or a Provider could execute "/system/identity/print"
            // We can resolve IRouterOsApiClient from DI if we need specific queries here
            
            result.Success = true;
            result.Reason = "تم الاتصال بنجاح";
            result.RouterIdentity = "MikroTik"; // Placeholder, can be improved to fetch actual
            result.RouterOSVersion = "Unknown";
            
            await _sessionManager.CloseSessionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test connection failed for {Host}", router.Host);
            result.Success = false;
            
            if (ex.Message.Contains("authenticate", StringComparison.OrdinalIgnoreCase) || 
                ex.Message.Contains("login", StringComparison.OrdinalIgnoreCase))
            {
                result.Reason = "فشل في تسجيل الدخول (تحقق من اسم المستخدم وكلمة المرور)";
            }
            else if (ex is OperationCanceledException || ex is TimeoutException)
            {
                result.Reason = "انتهى وقت الاتصال (تأكد من عنوان IP والمنفذ)";
            }
            else
            {
                result.Reason = $"خطأ غير معروف: {ex.Message}";
            }
        }
        finally
        {
            sw.Stop();
            result.LatencyMs = sw.ElapsedMilliseconds;
        }

        return result;
    }
}
