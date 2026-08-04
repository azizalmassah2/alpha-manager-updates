using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;

/// <summary>
/// Factory يختار مزود أوامر RouterOS المناسب بناءً على نوع الراوتر المتصل.
/// يستخدم RouterCapabilityService (مع Cache) — لا يُعيد الاستعلام.
/// يعتمد على DI: جميع المزودات مُحقَنة في Dictionary قابل للتوسع.
/// </summary>
public sealed class MikroTikCommandProviderFactory : IMikroTikCommandProviderFactory
{
    private readonly IRouterCapabilityService _capabilityService;
    private readonly IReadOnlyDictionary<RouterSystemType, IMikroTikCommandProvider> _providers;

    public MikroTikCommandProviderFactory(
        IRouterCapabilityService capabilityService,
        IEnumerable<IMikroTikCommandProvider> providers)
    {
        _capabilityService = capabilityService;

        // بناء Dictionary من جميع المزودات المُحقَنة عبر DI
        _providers = providers.ToDictionary(p => p.SystemType);
    }

    public async Task<IMikroTikCommandProvider> GetProviderAsync(CancellationToken ct = default)
    {
        var typeStr = await _capabilityService.GetProfileSystemTypeAsync(ct);

        var systemType = typeStr switch
        {
            "UMv7"    => RouterSystemType.UserManagerV7,
            "UMv6"    => RouterSystemType.UserManagerV6,
            "Hotspot" => RouterSystemType.Hotspot,
            _         => RouterSystemType.Unknown
        };

        // Fallback آمن: إذا لم يُعرف النوع نستخدم V6 (الأكثر توافقاً)
        if (_providers.TryGetValue(systemType, out var provider))
            return provider;

        if (_providers.TryGetValue(RouterSystemType.UserManagerV6, out var fallback))
            return fallback;

        throw new InvalidOperationException(
            $"No IMikroTikCommandProvider registered for RouterSystemType '{systemType}'. " +
            "Ensure all providers are registered in DependencyInjection.");
    }
}
