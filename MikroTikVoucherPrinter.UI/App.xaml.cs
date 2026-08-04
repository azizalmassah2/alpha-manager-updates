using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using MikroTikVoucherPrinter.Application;
using MikroTikVoucherPrinter.Infrastructure;
using MikroTikVoucherPrinter.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.UI.Services;
using MikroTikVoucherPrinter.UI.ViewModels;
using MikroTikVoucherPrinter.UI.ViewModels.Pages;
using MikroTikVoucherPrinter.UI.Views;

namespace MikroTikVoucherPrinter.UI;

/// <summary>
/// نقطة الدخول للتطبيق - إعداد DI + Serilog + Navigation
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        // إعداد Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LuxCard", "logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console()
            .CreateLogger();

        // إعداد Host مع DI
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // طبقة التطبيق
                services.AddApplicationServices();

                // طبقة البنية التحتية
                services.AddInfrastructureServices();

                // التنقل والحالة
                services.AddSingleton<INavigationState, NavigationState>();
                services.AddSingleton<NavigationService>();
                services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

                // الحوارات
                services.AddSingleton<IDialogService, DialogService>();

                // الثيمات
                services.AddSingleton<ThemeService>();
                services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<GenerateVoucherViewModel>();
                services.AddTransient<VoucherManagementViewModel>();  // Transient: يعتمد على IVoucherRepository (Scoped)
                services.AddTransient<SyncViewModel>();
                services.AddTransient<PrintCenterViewModel>();
                services.AddTransient<ProfileManagementViewModel>();
                services.AddTransient<AgentManagementViewModel>();
                services.AddTransient<TemplateManagementViewModel>();
                services.AddTransient<BatchManagementViewModel>();
                services.AddTransient<DbExplorerViewModel>();

                // النوافذ
                services.AddSingleton<MainWindow>();
                services.AddTransient<Views.LoginWindow>();
                services.AddTransient<Views.Pages.TemplateManagementPage>();
                services.AddTransient<Views.Pages.BatchManagementPage>();

            })
            .Build();

        SetupGlobalExceptionHandling();
    }

    private void SetupGlobalExceptionHandling()
    {
        // 1. استثناءات واجهة المستخدم (UI Thread)
        this.DispatcherUnhandledException += (s, e) =>
        {
            Log.Fatal(e.Exception, "حدث خطأ غير متوقع في واجهة المستخدم (Dispatcher)");
            ShowEmergencyErrorDialog("حدث خطأ غير متوقع في النظام. تم تسجيل المشكلة وأرفق التفاصيل.");
            e.Handled = true; // منع الانهيار الحتمي إذا أمكن
        };

        // 2. استثناءات Threads الخلفية
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal(ex, "حدث خطأ حرج (AppDomain) أدى إلى إنهاء التطبيق");
            ShowEmergencyErrorDialog("حدث خطأ حرج ويجب إغلاق التطبيق. سيتم حفظ التفاصيل.");
        };

        // 3. استثناءات Tasks غير المراقبة
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log.Error(e.Exception, "حدث خطأ في Task خلفي (UnobservedTask)");
            e.SetObserved(); // منع الانهيار
        };
    }

    private bool _isShowingError = false;

    private void ShowEmergencyErrorDialog(string message)
    {
        if (_isShowingError) return;
        _isShowingError = true;

        try
        {
            MessageBox.Show(
                message,
                "خطأ فادح في النظام - لوكس كارد",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        finally
        {
            _isShowingError = false;
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Information("═══════════════════════════════════════");
        Log.Information("   لوكس كارد - بدء التشغيل");
        Log.Information("   الإصدار: 1.0.0");
        Log.Information("═══════════════════════════════════════");

        try
        {
            await _host.StartAsync();

            // تحميل الإعدادات أولاً (لمعرفة الراوتر المحفوظ عند تهيئة القاعدة)
            var settingsService = _host.Services.GetRequiredService<ISettingsService>();
            await settingsService.LoadAsync();

            // تحميل الثيم المحفوظ
            var themeService = _host.Services.GetRequiredService<ThemeService>();
            themeService.LoadSavedTheme();

            // ══ منطق تسجيل الدخول ══
            var savedHost = settingsService.Get("MikroTik.Host", "");
            var savedUser = settingsService.Get("MikroTik.Username", "");
            var savedPass = settingsService.Get("MikroTik.Password", "");

            // دائماً أظهر شاشة الدخول أولاً كما طلب المستخدم
            var loginWindow = _host.Services.GetRequiredService<Views.LoginWindow>();
            bool? result = loginWindow.ShowDialog();

            if (result != true)
            {
                Log.Information("المستخدم أغلق شاشة الدخول. إغلاق التطبيق.");
                Shutdown();
                return;
            }

            // تهيئة قاعدة بيانات SQLite الخاصة بهذا الراوتر (بعد حفظ بيانات الدخول)
            try
            {
                using var scope = _host.Services.CreateScope();
                var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
                var dbLogger = loggerFactory.CreateLogger("LuxCard.Database");
                var factory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<MikroTikVoucherPrinter.Infrastructure.Data.LuxCardDbContext>>();
                await using var dbContext = await factory.CreateDbContextAsync();
                await dbContext.Database.EnsureCreatedAsync();
                await LuxCardSqliteSchemaUpgrade.ApplyAsync(dbContext, dbLogger);
                await BuiltInTemplateSeeder.EnsureSeedAsync(dbContext, dbLogger);

                var batchMigration = scope.ServiceProvider.GetRequiredService<Infrastructure.Services.BatchMigrationService>();
                await batchMigration.MigrateIfNeededAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "حدث خطأ أثناء تهيئة قاعدة البيانات المحلية.");
            }

            // إعداد التنقل
            var navigationService = _host.Services.GetRequiredService<NavigationService>();
            navigationService.RegisterPage<DashboardViewModel>("Dashboard");
            navigationService.RegisterPage<GenerateVoucherViewModel>("Generate");
            navigationService.RegisterPage<BatchManagementViewModel>("BatchManagement");
            navigationService.RegisterPage<VoucherManagementViewModel>("Management");
            navigationService.RegisterPage<SyncViewModel>("Sync");
            navigationService.RegisterPage<PrintCenterViewModel>("Print");
            navigationService.RegisterPage<ProfileManagementViewModel>("ProfileManagement");
            navigationService.RegisterPage<AgentManagementViewModel>("AgentManagement");
            navigationService.RegisterPage<TemplateManagementViewModel>("TemplateManagement");
            navigationService.RegisterPage<DbExplorerViewModel>("DbExplorer");
            navigationService.RegisterPage<SettingsViewModel>("Settings");

            // عرض النافذة الرئيسية (تعيين MainWindow ضروري بعد إغلاق نافذة الدخول)
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
            navigationService.NavigateTo("Dashboard");

            Log.Information("تم بدء التطبيق بنجاح");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "فشل بدء التطبيق");
            MessageBox.Show($"فشل بدء التطبيق: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task<bool> TryAutoConnectAsync(string host, string username, string password)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var conn = tik4net.ConnectionFactory.CreateConnection(tik4net.TikConnectionType.Api);
                conn.SendTimeout    = 4000;
                conn.ReceiveTimeout = 4000;
                conn.Open(host, username, password);
                Log.Information("✅ اتصال تلقائي ناجح بـ {Host}", host);
                return true;
            });
        }
        catch (Exception ex)
        {
            Log.Warning("⚠️ فشل الاتصال التلقائي بـ {Host}: {Error}", host, ex.Message);
            return false;
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("جاري إغلاق التطبيق...");

        try
        {
            // حفظ الإعدادات قبل الخروج
            var settingsService = _host.Services.GetRequiredService<ISettingsService>();
            await settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "فشل حفظ الإعدادات عند الخروج");
        }

        await _host.StopAsync();
        _host.Dispose();
        Log.CloseAndFlush();

        base.OnExit(e);
    }
}
