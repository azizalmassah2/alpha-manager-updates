using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Discovery;
using Lux.MikroTik.Interfaces;
using Lux.MikroTik.Monitoring;
using Lux.MikroTik.Providers;
using Lux.MikroTik.Services;
using Lux.Platform.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Lux.MikroTik;

public static class DependencyInjection
{
    public static IServiceCollection AddMikroTikServices(this IServiceCollection services, bool useMockProvider = false)
    {
        // Core Managers
        services.AddTransient<IMikroTikDeviceManager, MikroTikDeviceManager>();
        
        // Connectivity Layer
        services.AddSingleton<IRouterOsApiClient, RouterOsApiClient>();

        if (useMockProvider)
        {
            services.AddSingleton<MockRouterOsProvider>();
            services.AddSingleton<IRouterOsProvider>(sp => sp.GetRequiredService<MockRouterOsProvider>());
            services.AddSingleton<IRouterOsTextProvider>(sp => sp.GetRequiredService<MockRouterOsProvider>());
        }
        else
        {
            services.AddSingleton<RouterOsApiProvider>();
            services.AddSingleton<IRouterOsProvider>(sp => sp.GetRequiredService<RouterOsApiProvider>());
            services.AddSingleton<IRouterOsTextProvider>(sp => sp.GetRequiredService<RouterOsApiProvider>());
        }
        services.AddSingleton<IMikroTikConnection, MikroTikConnection>();
        services.AddSingleton<IMikroTikCommandExecutor, MikroTikCommandExecutor>();
        services.AddSingleton<IMikroTikSessionManager, MikroTikSessionManager>();
        
        // Discovery Layer
        services.AddScoped<IMikroTikDeviceInfoProvider, MikroTikDeviceInfoProvider>();
        services.AddScoped<IMikroTikDiscoveryService, MikroTikDiscoveryService>();
        
        // Monitoring Layer
        services.AddScoped<IMikroTikTelemetryProvider, MikroTikTelemetryProvider>();
        services.AddScoped<IDeviceMonitoringService, MikroTikMonitoringService>();
        
        // Backup Layer
        services.AddScoped<IDeviceBackupProvider, Lux.MikroTik.Backups.MikroTikBackupProvider>();
        
        // Configuration Layer
        services.AddTransient<IDeviceConfigurationProvider, MikroTikConfigurationProvider>();
        services.AddTransient<IDeviceFirmwareProvider, MikroTikFirmwareProvider>();
        
        // Future services like Hotspot, UserManager, PPPoE will be registered here.
        
        return services;
    }
}
