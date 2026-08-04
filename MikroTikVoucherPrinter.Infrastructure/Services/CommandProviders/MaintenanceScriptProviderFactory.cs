using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;

/// <summary>
/// Factory يختار مزود اسكريبتات الصيانة المناسب بناءً على نوع الراوتر المتصل.
/// يستخدم نفس RouterCapabilityService Cache من MikroTikCommandProviderFactory.
/// </summary>
public sealed class MaintenanceScriptProviderFactory : IMaintenanceScriptProviderFactory
{
    private readonly IRouterCapabilityService _capabilityService;
    private readonly IReadOnlyDictionary<RouterSystemType, IMaintenanceScriptProvider> _providers;

    public MaintenanceScriptProviderFactory(
        IRouterCapabilityService capabilityService,
        IEnumerable<IMaintenanceScriptProvider> providers)
    {
        _capabilityService = capabilityService;
        _providers = providers.ToDictionary(GetKeyForProvider);
    }

    private static RouterSystemType GetKeyForProvider(IMaintenanceScriptProvider provider)
        => provider switch
        {
            V6MaintenanceScriptProvider  => RouterSystemType.UserManagerV6,
            V7MaintenanceScriptProvider  => RouterSystemType.UserManagerV7,
            HotspotMaintenanceScriptProvider => RouterSystemType.Hotspot,
            _ => throw new InvalidOperationException($"Unknown maintenance script provider type: {provider.GetType().Name}")
        };

    public async Task<IMaintenanceScriptProvider> GetProviderAsync(CancellationToken ct = default)
    {
        var typeStr = await _capabilityService.GetProfileSystemTypeAsync(ct);

        var systemType = typeStr switch
        {
            "UMv7"    => RouterSystemType.UserManagerV7,
            "UMv6"    => RouterSystemType.UserManagerV6,
            "Hotspot" => RouterSystemType.Hotspot,
            _         => RouterSystemType.Unknown
        };

        if (_providers.TryGetValue(systemType, out var provider))
            return provider;

        // Fallback: V6 الأكثر توافقاً
        if (_providers.TryGetValue(RouterSystemType.UserManagerV6, out var fallback))
            return fallback;

        throw new InvalidOperationException(
            $"No IMaintenanceScriptProvider registered for RouterSystemType '{systemType}'.");
    }
}
