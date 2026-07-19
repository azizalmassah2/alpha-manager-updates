using System;
using Lux.OpenWrt.Interfaces;
using Lux.OpenWrt.Services;
using Lux.Platform.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Lux.OpenWrt;

public static class DependencyInjection
{
    public static IServiceCollection AddOpenWrtServices(this IServiceCollection services)
    {
        services.AddHttpClient<IUbusClient, UbusClient>();
        services.AddTransient<IUciService, UciService>();
        services.AddTransient<IDeviceDiscoveryService, DeviceDiscoveryService>();
        services.AddTransient<IOpenWrtDeviceManager, OpenWrtDeviceManager>();
        
        services.AddTransient<IHostnameConfigurationService, HostnameConfigurationService>();
        services.AddTransient<INetworkConfigurationService, NetworkConfigurationService>();
        services.AddTransient<IVlanConfigurationService, VlanConfigurationService>();
        services.AddTransient<IWirelessConfigurationService, WirelessConfigurationService>();
        services.AddTransient<ICommitApplyService, CommitApplyService>();
        services.AddTransient<IBackupRestoreService, BackupRestoreService>();
        services.AddTransient<IOpenWrtTelemetryProvider, OpenWrtTelemetryProvider>();
        services.AddTransient<IDeviceMonitoringService, DeviceMonitoringService>();
        services.AddTransient<IProgrammingRollbackService, ProgrammingRollbackService>();
        services.AddTransient<IProgrammingService, ProgrammingService>();
        services.AddTransient<IDeviceConfigurationProvider, Lux.OpenWrt.Providers.OpenWrtConfigurationProvider>();
        services.AddTransient<IDeviceFirmwareProvider, OpenWrtFirmwareProvider>();
        
        return services;
    }
}
