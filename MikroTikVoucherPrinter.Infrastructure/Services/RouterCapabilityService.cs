using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class RouterCapabilityService : IRouterCapabilityService
{
    private readonly IMikroTikCommandExecutor _commandExecutor;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly ILogger<RouterCapabilityService> _logger;
    private string? _cachedType;
    private Guid? _lastRouterId;

    public RouterCapabilityService(IMikroTikCommandExecutor commandExecutor, IActiveRouterContext activeRouterContext, ILogger<RouterCapabilityService> logger)
    {
        _commandExecutor = commandExecutor;
        _activeRouterContext = activeRouterContext;
        _logger = logger;
    }

    public async Task<string> GetProfileSystemTypeAsync(CancellationToken cancellationToken = default)
    {
        var currentRouterId = _activeRouterContext.CurrentRouterId;
        
        if (_cachedType != null && _lastRouterId == currentRouterId)
            return _cachedType;

        _lastRouterId = currentRouterId;

        // Determine capability via /system/package/print to avoid TikNoSuchCommandException breaking Visual Studio debugger.
        try
        {
            var response = await _commandExecutor.ExecuteAsync(new MikroTikCommand { Command = "/system/package/print" }, cancellationToken);
            bool hasUserManager = false;
            
            if (response.RawData != null)
            {
                foreach (var dict in response.RawData)
                {
                    if (dict.TryGetValue("name", out var pkgName))
                    {
                        if (pkgName.Contains("user-manager", StringComparison.OrdinalIgnoreCase) || 
                            pkgName.Contains("usermanager", StringComparison.OrdinalIgnoreCase))
                        {
                            hasUserManager = true;
                            break;
                        }
                    }
                }
            }
            
            if (hasUserManager)
            {
                var osVersion = _activeRouterContext.CurrentRouter?.RouterOSVersion ?? "";
                if (osVersion.StartsWith("7.") || osVersion.StartsWith("v7."))
                {
                    _cachedType = "UMv7";
                }
                else
                {
                    _cachedType = "UMv6";
                }
                return _cachedType;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to query /system/package/print: {Msg}", ex.Message);
        }

        // If no user-manager package, fallback to Hotspot (always available in RouterOS)
        _cachedType = "Hotspot";
        return _cachedType;
    }

    public void ClearCache()
    {
        _cachedType = null;
        _lastRouterId = null;
    }
}
