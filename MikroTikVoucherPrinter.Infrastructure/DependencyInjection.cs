using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Services;
using MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Infrastructure.Repositories.Platform;

namespace MikroTikVoucherPrinter.Infrastructure;

/// <summary>
/// تسجيل خدمات طبقة البنية التحتية في حاوية الحقن
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // إعدادات
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<ISecureStorageService, DpapiSecureStorageService>();

        // Platform Database Setup
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var platformFolder = Path.Combine(appData, "Lux Platform");
        Directory.CreateDirectory(platformFolder);
        var platformDbPath = Path.Combine(platformFolder, "platform.db");
        
        services.AddDbContextFactory<Data.PlatformDbContext>(options =>
            options.UseSqlite($"Data Source={platformDbPath}"));

        services.AddDbContextFactory<Data.LuxCardDbContext>(options =>
            options.UseSqlite($"Data Source={platformDbPath}"));

        services.AddSingleton<Application.Interfaces.IVoucherBackgroundImportManager, Services.VoucherBackgroundImportManager>();

        // Platform Repositories & Services
        services.AddSingleton<IActiveRouterContext, ActiveRouterContext>();
        services.AddScoped<IRouterRepository, RouterRepository>();
        services.AddScoped<ISyncQueueRepository, SyncQueueRepository>();
        services.AddScoped<MikroTikVoucherPrinter.Domain.Interfaces.Telemetry.ITelemetryRepository, MikroTikVoucherPrinter.Infrastructure.Repositories.Telemetry.TelemetryRepository>();

        // Repositories & Query Services
        services.AddScoped(typeof(IGenericRepository<>), typeof(Repositories.GenericRepository<>));
        services.AddScoped<IVoucherRepository, Repositories.VoucherRepository>();
        services.AddScoped<Application.Interfaces.IVoucherQueryService, Services.VoucherQueryService>();
        services.AddScoped<Application.Interfaces.ISalesQueryService, Services.SalesQueryService>();
        services.AddScoped<Application.Interfaces.IVlanTelemetryService, Services.VlanTelemetryService>();
        services.AddScoped<Application.Interfaces.IVoucherImportService, Services.VoucherImportService>();
        services.AddScoped<Application.Interfaces.IVoucherRestoreService, Services.VoucherRestoreService>();
        services.AddSingleton<Application.Interfaces.IVoucherCacheService, Services.VoucherCacheService>();
        services.AddSingleton<Application.Interfaces.IProfileCacheService, Services.ProfileCacheService>();

        // ══ Batch-Centric Architecture ══
        services.AddScoped<Domain.Interfaces.IBatchRepository, Repositories.BatchRepository>();
        services.AddScoped<Application.Interfaces.IBatchQueryService, Services.BatchQueryService>();
        services.AddScoped<Application.Interfaces.IBatchService, Services.BatchService>();
        services.AddScoped<Services.BatchMigrationService>();
        
        // Services (Printing & Migration)
        services.AddTransient<Templates.IPrintTemplate, Templates.A4GridTemplate>();
        services.AddTransient<Templates.IPrintTemplate, Templates.ThermalTemplate>();
        services.AddTransient<Templates.IPrintTemplate, Templates.HawaeGridTemplate>();
        
        services.AddScoped<LegacyMikroTikIntegrationService>();
        services.AddScoped<IMikroTikVoucherManager, MikroTikVoucherManager>();
        services.AddScoped<Application.Interfaces.IMikroTikIntegrationService, FeatureFlaggedMikroTikIntegrationService>();
        services.AddScoped<Application.Interfaces.ISyncService, Services.SyncService>();
        services.AddScoped<Application.Interfaces.IPrintService, Services.PrintService>();
        services.AddScoped<Application.Interfaces.IPrintPreviewService, Services.PrintPreviewService>();
        services.AddScoped<Application.Interfaces.IPrintJobService, Services.PrintJobService>();
        services.AddScoped<Application.Interfaces.ITemplateService, Services.TemplateService>();

        // ══ Lux Template Engine (v1.0) — موازٍ للنظام القديم ══
        services.AddScoped<Application.Interfaces.ILuxTemplateService, Services.LuxTemplateService>();
        services.AddScoped<Application.Interfaces.ITemplateEngine, Services.TemplateEngineService>();
        services.AddSingleton<Application.Interfaces.IRouterCapabilityService, Services.RouterCapabilityService>();
        services.AddScoped<Application.Interfaces.IProfileService, Services.ProfileService>();
        services.AddScoped<Application.Interfaces.IAgentService, Services.AgentService>();

        // ══ RouterOS Command Provider Layer ══════════════════════════════════════════
        // مزودات الأوامر — كل مزود يُسجَّل كـ IMikroTikCommandProvider
        services.AddSingleton<Application.Interfaces.IMikroTikCommandProvider, RouterOsV6CommandProvider>();
        services.AddSingleton<Application.Interfaces.IMikroTikCommandProvider, RouterOsV7CommandProvider>();
        services.AddSingleton<Application.Interfaces.IMikroTikCommandProvider, HotspotCommandProvider>();
        // Factory يختار المزود المناسب بناءً على RouterCapabilityService Cache
        services.AddSingleton<Application.Interfaces.IMikroTikCommandProviderFactory, MikroTikCommandProviderFactory>();

        // ══ Maintenance Script Provider Layer ════════════════════════════════════════
        // مزودات الاسكريبتات — كل مزود يُسجَّل كـ IMaintenanceScriptProvider
        services.AddSingleton<Application.Interfaces.IMaintenanceScriptProvider, V6MaintenanceScriptProvider>();
        services.AddSingleton<Application.Interfaces.IMaintenanceScriptProvider, V7MaintenanceScriptProvider>();
        services.AddSingleton<Application.Interfaces.IMaintenanceScriptProvider, HotspotMaintenanceScriptProvider>();
        // Factory يختار المزود المناسب بناءً على RouterCapabilityService Cache
        services.AddSingleton<Application.Interfaces.IMaintenanceScriptProviderFactory, MaintenanceScriptProviderFactory>();

        // ══ خدمة Provisioning المستقلة ═══════════════════════════════════════════
        // تستخدم IMikroTikCommandProviderFactory — لا ترث من LegacyMikroTikIntegrationService
        services.AddScoped<Services.MikroTikProvisioningService>();
        services.AddScoped<Services.MaintenanceService>();

        // Operations & Execution Framework
        services.AddSingleton<System.Threading.Channels.Channel<Domain.Entities.Operations.OperationJob>>(_ => 
            System.Threading.Channels.Channel.CreateUnbounded<Domain.Entities.Operations.OperationJob>());
        
        services.AddHostedService<Operations.BackgroundOperationWorker>();
        services.AddSingleton<IOperationEngine, Operations.OperationEngine>();
        services.AddSingleton<IOperationRegistry, Operations.OperationRegistry>();
        services.AddScoped<IOperationAuditService, Operations.OperationAuditService>();
        services.AddScoped<IOperationHistoryService, Operations.OperationHistoryService>();
        services.AddScoped<IBatchOperationService, Operations.BatchOperationService>();
        
        // Register Operations
        services.AddTransient<Application.Operations.Router.RouterBackupOperation>();
        services.AddTransient<Application.Operations.Modem.BatchModemRebootOperation>();

        // NOC Health Monitoring
        services.AddSingleton<Monitoring.IHealthMonitorProvider, Monitoring.PingHealthMonitorProvider>();
        services.AddHostedService<Monitoring.NocHealthMonitoringService>();

        return services;
    }
}
