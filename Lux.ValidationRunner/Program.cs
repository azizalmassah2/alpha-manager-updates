using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lux.MikroTik;
using MikroTikVoucherPrinter.Infrastructure;
using MikroTikVoucherPrinter.Infrastructure.Services;
using Lux.MikroTik.Providers;
using Lux.MikroTik.Interfaces;
using Lux.Platform.Abstractions.Interfaces;
using Moq;
using System.Threading.Tasks;

namespace Lux.ValidationRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== PHASE 1 VALIDATION RUNNER ===");
            
            // 1. Dependency Injection Verification
            Console.WriteLine("\n--- 1. DI VERIFICATION ---");
            var services = new ServiceCollection();
            
            // Add Logging
            services.AddLogging(configure => configure.AddConsole());

            // Mock ISettingsService to control Feature Flag
            var settingsMock = new Mock<ISettingsService>();
            settingsMock.Setup(s => s.Get("Connectivity.UseModernArchitecture", false)).Returns(true);
            services.AddSingleton<ISettingsService>(settingsMock.Object);
            
            // Add MikroTik core services
            services.AddMikroTikServices(useMockProvider: true); // We'll use mock for testing
            
            // Register infrastructure services manually instead of calling AddInfrastructureServices to avoid EF Core/SQLite dependencies
            services.AddScoped<LegacyMikroTikIntegrationService>();
            services.AddScoped<IMikroTikVoucherManager, MikroTikVoucherManager>();
            services.AddScoped<MikroTikVoucherPrinter.Application.Interfaces.IMikroTikIntegrationService, FeatureFlaggedMikroTikIntegrationService>();
            
            var provider = services.BuildServiceProvider();
            
            var integrationService = provider.GetService<MikroTikVoucherPrinter.Application.Interfaces.IMikroTikIntegrationService>();
            Console.WriteLine($"IMikroTikIntegrationService resolved: {integrationService?.GetType().Name ?? "NULL"}");
            
            var apiClient = provider.GetService<IRouterOsApiClient>();
            Console.WriteLine($"IRouterOsApiClient resolved: {apiClient?.GetType().Name ?? "NULL"}");
            
            var osProvider = provider.GetService<IRouterOsProvider>();
            Console.WriteLine($"IRouterOsProvider resolved: {osProvider?.GetType().Name ?? "NULL"}");

            var voucherManager = provider.GetService<IMikroTikVoucherManager>();
            Console.WriteLine($"IMikroTikVoucherManager resolved: {voucherManager?.GetType().Name ?? "NULL"}");

            var legacyService = provider.GetService<LegacyMikroTikIntegrationService>();
            Console.WriteLine($"LegacyMikroTikIntegrationService resolved: {legacyService?.GetType().Name ?? "NULL"}");

            // 2. Feature Flag routing verification
            Console.WriteLine("\n--- 2. FEATURE FLAG ROUTING VERIFICATION ---");
            try 
            {
                // To properly test routing, we would need to mock the underlying manager, but since we're using MockRouterOsProvider, 
                // calling CreateUserAsync will hit the mock and not actually dial out.
                Console.WriteLine("Executing CreateUserAsync via FeatureFlagged proxy with Modern=True...");
                var result = await integrationService.CreateUserAsync("testUser", "testPass", "testProfile");
                Console.WriteLine($"Result: {(result.IsSuccess ? "Success" : "Failed")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during routing: {ex.Message}");
            }

            // 3. Device Manager Verification
            Console.WriteLine("\n--- 3. DEVICE MANAGER VERIFICATION ---");
            var deviceManager = provider.GetService<IMikroTikDeviceManager>();
            try 
            {
                var status = await deviceManager.CheckStatusAsync();
                Console.WriteLine($"CheckStatusAsync result: {status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during CheckStatusAsync: {ex.Message}");
            }

            Console.WriteLine("\n=== END OF VALIDATION ===");
        }
    }
}
