using Lux.Management.Console.Core;
using Lux.Management.Console.ViewModels;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Management.Console.Themes;
using Moq;
using Xunit;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace Lux.Management.Console.Tests;

/// <summary>
/// MainViewModel navigation tests - kept as stubs since the navigation model
/// was updated in Phase 8.6. The old module-based commands (NavigateDashboard, NavigateFirmware)
/// were replaced by center-based navigation (MikroTikCenter, ModemsCenter, etc.)
/// </summary>
public class MainViewModelTests
{
    [Fact]
    public void MainViewModel_CanBeInstantiated()
    {
        // Arrange
        var permissionServiceMock = new Mock<IPermissionService>();
        var eventBusMock = new Mock<IEventBus>();
        var navigationServiceMock = new Mock<INavigationService>();
        var themeManagerMock = new Mock<IThemeManager>();
        var shellStateMock = new Mock<IShellState>();
        var busyIndicatorMock = new Mock<IBusyIndicatorService>();

        var activeRouterContextMock = new Mock<MikroTikVoucherPrinter.Domain.Interfaces.Platform.IActiveRouterContext>();
        var activeRouterStatusVm = new Lux.Management.Console.Core.ViewModels.ActiveRouterStatusViewModel(activeRouterContextMock.Object);

        var discoveryServiceMock = new Mock<Lux.Management.Console.Modules.MikroTik.Connections.Services.IMikroTikDiscoveryService>();
        var routerRepositoryMock = new Mock<MikroTikVoucherPrinter.Domain.Interfaces.Platform.IRouterRepository>();
        var secureStorageServiceMock = new Mock<ISecureStorageService>();
        var routerOsProviderMock = new Mock<Lux.MikroTik.Providers.IRouterOsProvider>();

        var connectionDialogVmMock = new Mock<Lux.Management.Console.Modules.MikroTik.Connections.Dialog.MikroTikConnectionDialogViewModel>(
            discoveryServiceMock.Object,
            activeRouterContextMock.Object,
            routerRepositoryMock.Object,
            secureStorageServiceMock.Object,
            routerOsProviderMock.Object);

        var settingsServiceMock = new Mock<MikroTikVoucherPrinter.Domain.Interfaces.ISettingsService>();
        var alertServiceMock = new Mock<IAlertService>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<MainViewModel>>();
        var sessionManagerMock = new Mock<Lux.Management.Console.Core.Session.ISessionManager>();
        var connectionServiceMock = new Mock<Lux.Management.Console.Core.Session.IConnectionService>();
        var routerSessionServiceMock = new Mock<Lux.Management.Console.Core.Session.IRouterSessionService>();
        var featureAuthorizationServiceMock = new Mock<Lux.Management.Console.Core.Security.Authorization.IFeatureAuthorizationService>();

        // Act
        var viewModel = new MainViewModel(
            permissionServiceMock.Object,
            eventBusMock.Object,
            navigationServiceMock.Object,
            themeManagerMock.Object,
            shellStateMock.Object,
            busyIndicatorMock.Object,
            activeRouterStatusVm,
            activeRouterContextMock.Object,
            connectionDialogVmMock.Object,
            settingsServiceMock.Object,
            routerRepositoryMock.Object,
            alertServiceMock.Object,
            loggerMock.Object,
            sessionManagerMock.Object,
            connectionServiceMock.Object,
            routerSessionServiceMock.Object,
            secureStorageServiceMock.Object,
            featureAuthorizationServiceMock.Object);

        // Assert
        Assert.NotNull(viewModel);
    }
}
