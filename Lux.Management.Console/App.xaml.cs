using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Lux.Management.Console.Core;
using Lux.Management.Console.Core.Session;
using Lux.Management.Console.Core.Security.Context;
using Lux.Management.Console.Core.Security.Session;
using Lux.Management.Console.Core.Security.Authorization;
using Lux.Management.Console.Core.Security.Runtime;
using Lux.Management.Console.Core.Security.Audit;
using Lux.Management.Console.Core.Security.Health;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Security.Trust;
using Lux.Management.Console.Core.Security.Crypto;
using Lux.Management.Console.Core.Security.Diagnostics;
using Lux.Management.Console.Core.Security.Policies;
using Lux.Management.Console.Core.Security.Runtime.Checks;
using Lux.Management.Console.Navigation;
using Lux.Management.Console.Services;
using Lux.Management.Console.Themes;
using Lux.Management.Console.ViewModels;
using Lux.Management.Console.Views;
using Lux.Management.Console.Modules.MikroTik.Dashboard;
using Lux.Management.Console.Modules.Monitoring;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.Views;
using Lux.Management.Console.Modules.MikroTik.UserManager.Printing.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Printing.Views;
using Lux.Management.Console.Modules.MikroTik.UserManager.Profiles.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Profiles.Views;
using Lux.Management.Console.Modules.MikroTik.UserManager.Agents.ViewModels;
using Lux.Management.Console.Modules.MikroTik.UserManager.Agents.Views;
using Lux.Management.Console.Modules.Settings.ViewModels;
using Lux.Management.Console.Modules.Settings.Views;
using Lux.Management.Console.Modules._Migration;
using Lux.Management.Console.Modules.MikroTik.ViewModels;
using Lux.Management.Console.Modules.MikroTik.Connections.ViewModels;
using Lux.Management.Console.Modules.MikroTik.Connections.Services;
using Lux.Management.Console.Modules.MikroTik.Backups.ViewModels;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Services;

using Lux.Management.Console.Modules.MikroTik.Views;
using Lux.Management.Console.Modules.MikroTik.Connections.Views;
using Lux.Management.Console.Modules.MikroTik.Backups.Views;
using Lux.Management.Console.Modules.MikroTik.RouterManagement.Views;
using Lux.Management.Console.Core.Views;
using Lux.Management.Console.Core.ViewModels;
using Lux.Management.Console.Modules.MikroTik.Hotspot.ViewModels;
using Lux.Management.Console.Modules.MikroTik.Hotspot.Views;
using Lux.Management.Console.Modules.Broadcasting.ViewModels;
using Lux.Management.Console.Modules.Broadcasting.Views;
using QuickMainViewModel = Lux.Management.Console.Modules.Broadcasting.QuickConfig.ViewModels.MainViewModel;
using QuickSettingsViewModel = Lux.Management.Console.Modules.Broadcasting.QuickConfig.ViewModels.SettingsViewModel;
using QuickConfigPage = Lux.Management.Console.Modules.Broadcasting.QuickConfig.Views.QuickConfigPage;
using QuickSettingsWindow = Lux.Management.Console.Modules.Broadcasting.QuickConfig.Views.SettingsWindow;
using QuickScanNetworksWindow = Lux.Management.Console.Modules.Broadcasting.QuickConfig.Views.ScanNetworksWindow;
using QuickPreviewWindow = Lux.Management.Console.Modules.Broadcasting.QuickConfig.Views.PreviewWindow;


using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Models;
using MikroTikVoucherPrinter.Application.State;
using MikroTikVoucherPrinter.Infrastructure.Monitoring;
using MikroTikVoucherPrinter.Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Infrastructure.Services;

namespace Lux.Management.Console;

public partial class App : Application
{
    private readonly IHost _host;
    public IServiceProvider ServiceProvider => _host.Services;

    public App()
    {
        // ── Arabic UI + English (Western) Digits — Global Fix ──────────────────
        // Keep ar-YE for RTL layout, Arabic text, and calendar — but force all
        // numeric display to use Western digits (0-9) instead of (٠١٢٣٤٥٦٧٨٩).
        //
        // Three layers are required to cover all WPF rendering paths:
        //   1. Thread culture    — controls string formatting (ToString, StringFormat)
        //   2. FrameworkElement.LanguageProperty — controls WPF's xml:lang, which
        //      the NumberSubstitution system reads to decide digit shapes
        //   3. NumberSubstitution.SubstitutionProperty default — explicitly disables
        //      digit substitution for every control in the visual tree

        var arabicCulture = new CultureInfo("ar-YE");

        // Clone and patch NumberFormat so digit grouping uses Western chars
        var numberFormat = (NumberFormatInfo)arabicCulture.NumberFormat.Clone();
        numberFormat.DigitSubstitution = DigitShapes.None; // 0-9 only
        arabicCulture.NumberFormat = numberFormat;         // requires a CultureInfo clone

        // Rebuild as a writable clone (CultureInfo("ar-YE") is read-only for NumberFormat)
        var culture = CultureInfo.CreateSpecificCulture("ar-YE");
        culture.NumberFormat.DigitSubstitution = DigitShapes.None;
        culture.NumberFormat.NativeDigits = new[] { "0","1","2","3","4","5","6","7","8","9" };

        Thread.CurrentThread.CurrentCulture   = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture   = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Override WPF's xml:lang default to en-US so NumberSubstitution sees a
        // Latin language tag and renders digits as Western in ALL TextBlocks/controls.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                System.Windows.Markup.XmlLanguage.GetLanguage(
                    CultureInfo.InvariantCulture.IetfLanguageTag)));



        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register Core Services
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IRegionManager, RegionManager>(); // Keep for backward compat for now
                services.AddSingleton<IEventBus, EventBus>();
                services.AddSingleton<ILegacyScreenMigrationService, LegacyScreenMigrationService>();
                services.AddSingleton<IThemeManager, ThemeManager>();
                services.AddSingleton<IUserNotificationService, UserNotificationService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IDispatcherService, DispatcherService>();
                services.AddSingleton<IClipboardService, ClipboardService>();
                services.AddSingleton<IUserContext, MockUserContext>();
                services.AddSingleton<IPermissionService, MockPermissionService>();
                services.AddSingleton<IAuditService, MockAuditService>();
                services.AddSingleton<IAutoRefreshService, AutoRefreshService>();
                services.AddSingleton<IShellState, ShellState>();
                services.AddSingleton<ISelectionContext, SelectionContext>();
                services.AddSingleton<IBusyIndicatorService, BusyIndicatorService>();
                services.AddSingleton<IVoucherPageStateTracker, VoucherPageStateTracker>();

                // Register Legacy Domain Services
                MikroTikVoucherPrinter.Application.DependencyInjection.AddApplicationServices(services);
                MikroTikVoucherPrinter.Infrastructure.DependencyInjection.AddInfrastructureServices(services);
                services.AddScoped<IRouterDataMigrationService, RouterDataMigrationService>();

                // Register Modern MikroTik Services
                Lux.MikroTik.DependencyInjection.AddMikroTikServices(services, useMockProvider: false);

                // Monitoring Services
                services.AddSingleton<IAlertService, InMemoryAlertService>();
                services.AddSingleton<IDeviceMetricsStore, InMemoryDeviceMetricsStore>();
                services.AddSingleton<IFleetOperationService, MockFleetOperationService>();
                
                // Connection Testing Services
                services.AddTransient<Lux.Management.Console.Modules.MikroTik.Connections.Services.IConnectionTestService, Lux.Management.Console.Modules.MikroTik.Connections.Services.RouterConnectionTestService>();

                // Register State Services
                services.AddSingleton<IDeviceRepository, InMemoryDeviceRepository>();
                services.AddSingleton<IDeviceHealthEvaluator, DeviceHealthEvaluator>();
                services.AddSingleton<IDeviceStateManager, DeviceStateManager>();

                // Register ViewModels
                services.AddTransient<ActiveRouterStatusViewModel>();
                services.AddTransient<MainViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<Lux.Management.Console.Modules.Home.ViewModels.HomeViewModel>();

                // Register Broadcasting & QuickConfig Services
                services.AddScoped<IBroadcastingService, BroadcastingService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.ILoggerService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.LoggerService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.IUbusClient, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.UbusClient>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.IUciService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.UciService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.IDeviceDiscoveryService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.DeviceDiscoveryService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.INetworkService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.NetworkService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.IWirelessService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.WirelessService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.IBackupService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.BackupService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.ITemplateService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.TemplateService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.ISavedNetworkService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.SavedNetworkService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces.IProgrammingService, Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.ProgrammingService>();
                services.AddSingleton<Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.AppSettingsService>();

                // Register Broadcasting & QuickConfig ViewModels
                services.AddTransient<BroadcastingCenterViewModel>();
                services.AddTransient<BroadcastingNeighborsViewModel>();
                services.AddTransient<BroadcastingMaintenanceViewModel>();
                services.AddTransient<BroadcastingFlashingViewModel>();
                services.AddTransient<QuickMainViewModel>();
                services.AddTransient<QuickSettingsViewModel>();

                // Register Broadcasting & QuickConfig Views
                services.AddTransient<BroadcastingCenterPage>();
                services.AddTransient<BroadcastingNeighborsPage>();
                services.AddTransient<BroadcastingMaintenancePage>();
                services.AddTransient<BroadcastingFlashingPage>();
                services.AddTransient<QuickConfigPage>();
                services.AddTransient<QuickSettingsWindow>();
                services.AddTransient<QuickScanNetworksWindow>();
                services.AddTransient<QuickPreviewWindow>();

                services.AddTransient<DevicesMonitorViewModel>();
                services.AddTransient<AlertsViewModel>();
                
                // MikroTik Connection Module Services
                services.AddSingleton<Lux.Management.Console.Modules.MikroTik.Connections.Services.IMikroTikDiscoveryService, Lux.Management.Console.Modules.MikroTik.Connections.Services.MikroTikDiscoveryService>();
                services.AddSingleton<Lux.Management.Console.Modules.MikroTik.Connections.Dialog.MikroTikConnectionDialogViewModel>();
                services.AddScoped<IConnectionTestService, RouterConnectionTestService>();
                services.AddSingleton<Lux.Management.Console.Modules.MikroTik.Connections.Services.IRouterDialogService, Lux.Management.Console.Modules.MikroTik.Connections.Services.RouterDialogService>();
                services.AddTransient<ConnectionsViewModel>();

                // MikroTik Router Management Services
                services.AddSingleton<IRouterHealthService, RouterHealthService>();
                services.AddHostedService(provider => (RouterHealthService)provider.GetRequiredService<IRouterHealthService>());
                services.AddTransient<IRouterManagementService, RouterManagementService>();
                services.AddSingleton<IDevicePingService, DevicePingService>();

                // ── Startup Services: Update + License ──────────────────────────────
                services.AddSingleton<IUpdateService, UpdateService>();
                services.AddSingleton<ILicenseService, LicenseService>();
                services.AddTransient<LicenseViewModel>();
                services.AddTransient<UpdatesViewModel>();
                services.AddTransient<AboutViewModel>();

                // ── خدمات دورة حياة الجلسة وتسجيل الدخول الجديدة ──────────────────
                services.AddSingleton<ISessionManager, SessionManager>();
                services.AddSingleton<IRouterSessionService, RouterSessionService>();
                services.AddSingleton<IConnectionService, ConnectionService>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<LoginWindow>();

                 // ── خدمات الأمان والتحقق والوقاية ──────────────────────────────────
                services.AddSingleton<IMemoryProtectionService, MemoryProtectionService>();
                services.AddSingleton<ISecurityAuditService, SecurityAuditService>();
                services.AddSingleton<IPublicKeyProvider, PublicKeyProvider>();
                services.AddSingleton<RouterTrustVerifier>();
                
                // تسجيل SecurityContext ليعرض واجهتين Singleton متطابقتين
                services.AddSingleton<SecurityContext>();
                services.AddSingleton<ISecurityContext>(sp => sp.GetRequiredService<SecurityContext>());
                services.AddSingleton<ISecurityContextUpdater>(sp => sp.GetRequiredService<SecurityContext>());

                services.AddSingleton<ISecurityHealthService, SecurityHealthService>();
                services.AddSingleton<ISessionSecurityService, SessionSecurityService>();
                services.AddSingleton<IFeatureAuthorizationService, FeatureAuthorizationService>();
                services.AddSingleton<IAntiTamperService, AntiTamperService>();

                // الناشر المركزي للأحداث الأمنية — يفك الارتباط بين المراقب وخدمة التدقيق
                services.AddSingleton<ISecurityEventPublisher, SecurityEventPublisher>();

                // تسجيل الفحوصات الأمنية الفردية ليستهلكها المراقب ديناميكياً
                services.AddSingleton<ISecurityCheck, DebuggerCheck>();
                services.AddSingleton<ISecurityCheck, SessionValidationCheck>();
                services.AddSingleton<ISecurityCheck, IntegrityCheck>();
                services.AddSingleton<ISecurityCheck, TamperCheck>();

                services.AddSingleton<IRuntimeMonitor, RuntimeMonitor>();

                // السياسات الأمنية — مُحقونة كـ Singletons لسهولة الاختبار والتوسع
                services.AddSingleton(new SessionPolicy());
                services.AddSingleton(new AuditPolicy());
                services.AddSingleton(new RuntimePolicy());
                services.AddSingleton(new AuthorizationPolicy());
                services.AddSingleton(new MemoryPolicy());

                // Hotspot Service & ViewModel
                services.AddSingleton<IHotspotService, HotspotService>();
                services.AddTransient<HotspotLoginViewModel>();

                // [PHASE-5] ViewModels are registered as Transient to avoid captive dependency validation errors on Scoped services.
                // High performance is guaranteed via Phase 2 Lazy Loading (deferring data queries to page activation).
                services.AddTransient<MikroTikCenterViewModel>();
                services.AddTransient<VoucherManagementViewModel>();
                services.AddTransient<Lux.Management.Console.Modules.MikroTik.UserManager.Sales.SalesViewModel>();
                services.AddTransient<ProfileManagementViewModel>();
                services.AddTransient<AgentManagementViewModel>();

                // Router Management ViewModels
                services.AddTransient<RouterDashboardViewModel>();
                services.AddTransient<SystemResourcesViewModel>();
                services.AddTransient<InterfacesViewModel>();
                services.AddTransient<IpAddressesViewModel>();
                services.AddTransient<RoutesViewModel>();
                services.AddTransient<DnsViewModel>();
                services.AddTransient<NtpViewModel>();
                services.AddTransient<BackupsViewModel>();
                services.AddTransient<Lux.Management.Console.Modules.MikroTik.RouterManagement.ViewModels.RouterOperationsViewModel>();
                services.AddTransient<NocViewModel>();
                services.AddTransient<RouterManagementCenterViewModel>();

                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SyncViewModel>();
                services.AddTransient<TemplateManagementViewModel>();
                services.AddTransient<Lux.Management.Console.Modules.Settings.ViewModels.DbExplorerViewModel>();
                
                // Operations Center
                services.AddTransient<Lux.Management.Console.Modules.Operations.ViewModels.OperationsCenterViewModel>();
                services.AddTransient<Lux.Management.Console.Modules.Operations.ViewModels.RouterOperationsViewModel>();
                services.AddTransient<Lux.Management.Console.Modules.Operations.ViewModels.ModemOperationsViewModel>();
                services.AddTransient<Lux.Management.Console.Modules.Operations.ViewModels.WirelessOperationsViewModel>();
                services.AddTransient<Lux.Management.Console.Modules.Operations.ViewModels.OperationHistoryViewModel>();

                // Register Views
                services.AddTransient<MainWindow>();
                services.AddTransient<DashboardPage>();
                services.AddTransient<Lux.Management.Console.Modules.Home.Views.HomePage>();
                services.AddTransient<MikroTikCenterPage>();
                services.AddTransient<DevicesMonitorPage>();
                services.AddTransient<AlertsPage>();
                services.AddTransient<VoucherManagementPage>();
                services.AddTransient<HotspotLoginPage>();
                
                services.AddTransient<ActiveRouterStatusView>();
                services.AddTransient<ConnectionsPage>();
                services.AddTransient<BackupsPage>();
                services.AddTransient<Lux.Management.Console.Modules.MikroTik.RouterManagement.Views.RouterOperationsPage>();

                services.AddTransient<ProfileManagementPage>();
                services.AddTransient<AgentManagementPage>();
                services.AddTransient<SettingsPage>();
                services.AddTransient<SyncPage>();
                services.AddTransient<TemplateManagementPage>();
                services.AddTransient<Lux.Management.Console.Modules.Settings.Views.DbExplorerPage>();
                
                services.AddTransient<Lux.Management.Console.Modules.Operations.Views.OperationsCenterPage>();
            })
            .Build();
    }

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        // تسجيل موفر صفحات الأكواد لدعم الترميزات العربية (Windows-1256)
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        // ── 1. عرض Splash Screen فوراً قبل أي شيء ─────────────────────────────
        var splash = new SplashWindow();
        splash.Show();

        try
        {
            // ── 2. بدء الـ Host ─────────────────────────────────────────────────
            splash.UpdateStatus("⚙️ تهيئة الخدمات...", 1);
            _host.Start();

            // تهيئة موصل الخدمات للضبط السريع
            Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers.ServiceLocator.Instance.Initialize(_host.Services);

            // ── 3. تهجير قواعد البيانات في خيط خلفي (لا تجميد للواجهة) ─────────
            splash.UpdateStatus("🗄️ تحديث قاعدة البيانات...", 2);
            await Task.Run(async () =>
            {
                using var scope = _host.Services.CreateScope();
                var loggerFactory = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
                var dbLogger = loggerFactory.CreateLogger("LuxCard.Database");

                var platformDb = scope.ServiceProvider.GetRequiredService<MikroTikVoucherPrinter.Infrastructure.Data.PlatformDbContext>();
                await platformDb.Database.MigrateAsync();
                await platformDb.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""VlanMonitoringConfigs"" (
                        ""RouterId"" TEXT NOT NULL,
                        ""VlanId"" TEXT NOT NULL,
                        ""DeviceIp"" TEXT NOT NULL,
                        ""Description"" TEXT NULL,
                        ""EnableMonitoring"" INTEGER NOT NULL,
                        PRIMARY KEY (""RouterId"", ""VlanId"")
                    );
                ");

                await platformDb.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""BroadcastingDevices"" (
                        ""Id"" TEXT NOT NULL PRIMARY KEY,
                        ""DisplayName"" TEXT NOT NULL,
                        ""IpAddress"" TEXT NULL,
                        ""MacAddress"" TEXT NULL,
                        ""DeviceType"" TEXT NULL,
                        ""Vendor"" TEXT NULL,
                        ""Username"" TEXT NULL,
                        ""Password"" TEXT NULL,
                        ""Notes"" TEXT NULL,
                        ""RouterId"" TEXT NOT NULL,
                        ""CreatedAt"" TEXT NULL,
                        ""UpdatedAt"" TEXT NULL,
                        ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                        ""RowVersion"" BLOB NULL
                    );
                ");

                // دالة مساعدة لإضافة الأعمدة بأمان دون توليد استثناءات SQLite
                async Task AddColumnIfNotExistsAsync(string tableName, string columnName, string columnDefinition)
                {
                    try
                    {
                        var conn = platformDb.Database.GetDbConnection();
                        bool opened = false;
                        if (conn.State != System.Data.ConnectionState.Open)
                        {
                            await conn.OpenAsync();
                            opened = true;
                        }
                        using (var checkCmd = conn.CreateCommand())
                        {
                            checkCmd.CommandText = $"PRAGMA table_info({tableName});";
                            using (var reader = await checkCmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var name = reader["name"]?.ToString();
                                    if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (opened) conn.Close();
                                        return; // العمود موجود بالفعل، تخطي
                                    }
                                }
                            }
                        }
                        
                        await platformDb.Database.ExecuteSqlRawAsync($@"ALTER TABLE ""{tableName}"" ADD COLUMN ""{columnName}"" {columnDefinition};");
                        if (opened) conn.Close();
                    }
                    catch { }
                }

                await AddColumnIfNotExistsAsync("BroadcastingDevices", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
                await AddColumnIfNotExistsAsync("BroadcastingDevices", "RowVersion", "BLOB NULL");
                await AddColumnIfNotExistsAsync("Routers", "UserManagerDbPath", "TEXT NULL");

                var factory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<MikroTikVoucherPrinter.Infrastructure.Data.LuxCardDbContext>>();
                await using var dbContext = await factory.CreateDbContextAsync();
                await MikroTikVoucherPrinter.Infrastructure.Data.LuxCardSqliteSchemaUpgrade.PreMigrateBootstrapAsync(dbContext, dbLogger);
                await dbContext.Database.MigrateAsync();

                var routerDataMigration = scope.ServiceProvider.GetRequiredService<IRouterDataMigrationService>();
                await routerDataMigration.MigrateNullRouterIdsAsync();
                await routerDataMigration.MigrateNullSystemTypesAsync();

                await MikroTikVoucherPrinter.Infrastructure.Data.LuxCardSqliteSchemaUpgrade.ApplyAsync(dbContext, dbLogger);
                await MikroTikVoucherPrinter.Infrastructure.Data.BuiltInTemplateSeeder.EnsureSeedAsync(dbContext, dbLogger);
            });

            // ── 4. فحص التحديثات (صامت — لا يوقف التشغيل عند الفشل) ────────────
            splash.UpdateStatus("🔄 فحص التحديثات...", 3);
            UpdateCheckResult? updateResult = null;
            try
            {
                var updateService = _host.Services.GetRequiredService<IUpdateService>();
                updateResult = await updateService.CheckForUpdateAsync();
            }
            catch { /* فشل صامت — التحديث اختياري */ }

            // ── 5. عرض نافذة التحديث (قبل فتح LoginWindow) ─────────────────────────
            if (updateResult != null && updateResult.HasUpdate)
            {
                // إغلاق Splash أولاً وتأكيد الإغلاق لمنع التداخل البصري
                splash.Close();
                // انتظار معالجة رسائل إغلاق النافذة في الـ Dispatcher
                await splash.Dispatcher.InvokeAsync(() => {}, System.Windows.Threading.DispatcherPriority.Background);
                await Task.Delay(100); // تأخير إضافي بسيط لضمان اختفاء النافذة تماماً

                var updateService = _host.Services.GetRequiredService<IUpdateService>();
                var updateDialog  = new UpdateDialog(updateResult, updateService);
                
                updateDialog.ShowDialog(); // ← تنتظر قرار المستخدم هنا

                // إذا كان التحديث إجبارياً والمستخدم لم يكمل التحديث (مثلاً أغلق النافذة)
                if (updateResult.MustUpdate && !updateDialog.UserChoseUpdate)
                {
                    Shutdown(0);
                    return;
                }
            }

            // ── 6. تشغيل نافذة تسجيل الدخول ─────────────────────────────────────
            splash.UpdateStatus("✅ جاهز!", 4);
            await Task.Delay(400);

            // إغلاق Splash
            splash.Close();

            // فتح نافذة تسجيل الدخول
            var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
            var loginVm = (LoginViewModel)loginWindow.DataContext;

            loginVm.LoginSucceeded += (session) =>
            {
                // عند نجاح الدخول:
                // 1. تشغيل الخدمات الخلفية المرتبطة بالراوتر النشط
                if (session.IsConnected)
                {
                    var refreshService = _host.Services.GetRequiredService<IAutoRefreshService>();
                    refreshService.Start();
                    _ = _host.Services.GetRequiredService<IAlertService>();
                    _ = _host.Services.GetRequiredService<IDeviceMetricsStore>();

                    // بدء تشغيل خيط المراقبة الأمنية لبيئة تشغيل البرنامج
                    var runtimeMonitor = _host.Services.GetRequiredService<IRuntimeMonitor>();
                    _ = runtimeMonitor.StartAsync(session);
                }

                // 2. إنشاء وفتح النافذة الرئيسية (Dashboard)
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();

                // التحقق من اقتراب انتهاء الترخيص وإظهار تنبيه
                if (session.IsProMode && session.LicenseExpiresAt.HasValue)
                {
                    DateTime expiryLocal = session.LicenseExpiresAt.Value.ToLocalTime().Date;
                    int daysRemaining = (expiryLocal - DateTime.Today).Days;
                    if (daysRemaining >= 0 && daysRemaining < 10)
                    {
                        var notificationService = _host.Services.GetRequiredService<IUserNotificationService>();
                        notificationService.ShowWarning(
                            $"⚠️ تنبيه: الترخيص الخاص بك أوشك على الانتهاء! متبقي له {daysRemaining} يوم/أيام فقط (ينتهي بتاريخ {expiryLocal:yyyy/MM/dd}). يرجى تجديد الترخيص لضمان استمرار تشغيل الوضع الاحترافي.",
                            "تنبيه انتهاء الترخيص");
                    }
                }

                // 3. إغلاق نافذة تسجيل الدخول
                loginWindow.Close();
            };

            loginWindow.Show();
        }
        catch (Exception ex)
        {
            // كتابة تفاصيل الخطأ في ملف للتشخيص
            try
            {
                string logPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
                System.IO.File.WriteAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STARTUP ERROR\n" +
                    $"Type:    {ex.GetType().FullName}\n" +
                    $"Message: {ex.Message}\n" +
                    $"Stack:   {ex.StackTrace}\n" +
                    (ex.InnerException != null
                        ? $"Inner:   {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n"
                        : ""));
            }
            catch { /* فشل حفظ السجل — لا تُسبّب استثناءً متداخلاً */ }

            splash.Close();
            MessageBox.Show(
                $"فشل تشغيل البرنامج:\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
                $"تم حفظ تفاصيل الخطأ في:\n{AppDomain.CurrentDomain.BaseDirectory}startup_error.log",
                "خطأ في بدء التشغيل",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }



    // ShowUpdateNotification تم حذفها — التحديث يُعرض الآن فوق Splash قبل فتح MainWindow

    private async void OnExit(object sender, ExitEventArgs e)
    {
        Microsoft.Extensions.Logging.ILogger<App>? logger = null;
        try
        {
            logger = _host.Services.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
        }
        catch { }

        try
        {
            var autoRefresh = _host.Services.GetService<IAutoRefreshService>();
            autoRefresh?.Stop();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error stopping AutoRefreshService during application exit.");
        }

        try
        {
            var runtimeMonitor = _host.Services.GetService<IRuntimeMonitor>();
            if (runtimeMonitor != null)
            {
                await runtimeMonitor.StopAsync();
                runtimeMonitor.Dispose();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error stopping RuntimeMonitor during application exit.");
        }

        try
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error stopping host during application exit.");
        }
    }
}
