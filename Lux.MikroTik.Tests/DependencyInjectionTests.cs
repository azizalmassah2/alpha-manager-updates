using System;
using Lux.MikroTik;
using Lux.MikroTik.Interfaces;
using Lux.MikroTik.Services;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Providers;
using Lux.Platform.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Lux.MikroTik.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddMikroTikServices_RegistersExpectedServices_WithMockProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Stub Domain dependency to test DI in isolation
        services.AddSingleton(new Mock<IDeviceMonitoringService>().Object);
        services.AddSingleton(new Mock<IDeviceTelemetryProvider>().Object);

        // Act
        services.AddMikroTikServices(useMockProvider: true);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IMikroTikDeviceManager>());
        var routerProvider = provider.GetService<IRouterOsProvider>();
        Assert.NotNull(routerProvider);
        Assert.IsType<MockRouterOsProvider>(routerProvider);
        Assert.NotNull(provider.GetService<IMikroTikConnection>());
        Assert.NotNull(provider.GetService<IMikroTikCommandExecutor>());
        Assert.NotNull(provider.GetService<IMikroTikSessionManager>());
        Assert.NotNull(provider.GetService<IMikroTikDeviceInfoProvider>());
        Assert.NotNull(provider.GetService<IMikroTikDiscoveryService>());
        Assert.NotNull(provider.GetService<IMikroTikTelemetryProvider>());
        Assert.NotNull(provider.GetService<IDeviceMonitoringService>());
    }

    [Fact]
    public void AddMikroTikServices_RegistersExpectedServices_WithApiProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Stub Domain dependency
        services.AddSingleton(new Mock<IDeviceMonitoringService>().Object);
        services.AddSingleton(new Mock<IDeviceTelemetryProvider>().Object);

        // Act
        services.AddMikroTikServices(useMockProvider: false);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IRouterOsApiClient>());
        var routerProvider = provider.GetService<IRouterOsProvider>();
        Assert.NotNull(routerProvider);
        Assert.IsType<RouterOsApiProvider>(routerProvider);
    }
}
