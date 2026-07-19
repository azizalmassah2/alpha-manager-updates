using Microsoft.Extensions.DependencyInjection;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Services;

namespace MikroTikVoucherPrinter.Application;

/// <summary>
/// تسجيل خدمات طبقة التطبيق في حاوية الحقن
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUnifiedBackupService, UnifiedBackupService>();
        services.AddScoped<IUnifiedConfigurationService, UnifiedConfigurationService>();
        services.AddScoped<ITemplateResolutionService, TemplateResolutionService>();
        services.AddScoped<IProvisioningOrchestrator, ProvisioningOrchestrator>();
        services.AddScoped<IUnifiedFirmwareService, UnifiedFirmwareService>();
        services.AddSingleton<IOperationHistoryRepository, InMemoryOperationHistoryRepository>();
        services.AddScoped<IFleetOperationService, FleetOperationService>();
        services.AddScoped<IVoucherGenerationService, VoucherGenerationService>();
        services.AddScoped<IVoucherManagementService, VoucherManagementService>();
        
        return services;
    }
}
