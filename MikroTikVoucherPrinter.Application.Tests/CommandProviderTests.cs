using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;
using Moq;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class CommandProviderTests
{
    [Fact]
    public void RouterOsV6CommandProvider_BuildsCorrectV6Commands()
    {
        // Arrange
        var provider = new RouterOsV6CommandProvider();

        // Act
        var addCmd = provider.BuildUserAddCommand("user1", "pass123", "admin");
        var assignCmd = provider.BuildAssignProfileCommand("user1", "1Hour", "admin");
        var printCmd = provider.BuildUserPrintCommand("user1");
        var removeCmd = provider.BuildUserRemoveCommand("*1A");

        // Assert
        Assert.Equal(RouterSystemType.UserManagerV6, provider.SystemType);
        Assert.Equal("/tool/user-manager/user/add", addCmd.Path);
        Assert.Equal("admin", addCmd.Parameters["customer"]);
        Assert.Equal("user1", addCmd.Parameters["username"]);
        Assert.Equal("pass123", addCmd.Parameters["password"]);

        Assert.Equal("/tool/user-manager/user/create-and-activate-profile", assignCmd.Path);
        Assert.Equal("admin", assignCmd.Parameters["customer"]);
        Assert.Equal("user1", assignCmd.Parameters["user"]);
        Assert.Equal("1Hour", assignCmd.Parameters["profile"]);

        Assert.Equal("/tool/user-manager/user/print", printCmd.Path);
        Assert.Equal("user1", printCmd.Parameters["username"]);

        Assert.Equal("/tool/user-manager/user/remove", removeCmd.Path);
        Assert.Equal("*1A", removeCmd.Parameters["numbers"]);
    }

    [Fact]
    public void RouterOsV7CommandProvider_BuildsCorrectV7Commands()
    {
        // Arrange
        var provider = new RouterOsV7CommandProvider();

        // Act
        var addCmd = provider.BuildUserAddCommand("user2", "pass456", "admin");
        var assignCmd = provider.BuildAssignProfileCommand("user2", "1Hour", "admin");
        var printCmd = provider.BuildUserPrintCommand("user2");
        var removeCmd = provider.BuildUserRemoveCommand("*2B");

        // Assert
        Assert.Equal(RouterSystemType.UserManagerV7, provider.SystemType);
        Assert.Equal("/user-manager/user/add", addCmd.Path);
        Assert.Equal("user2", addCmd.Parameters["name"]);
        Assert.Equal("pass456", addCmd.Parameters["password"]);

        Assert.Equal("/user-manager/user/profile/add", assignCmd.Path);
        Assert.Equal("user2", assignCmd.Parameters["user"]);
        Assert.Equal("1Hour", assignCmd.Parameters["profile"]);

        Assert.Equal("/user-manager/user/print", printCmd.Path);
        Assert.Equal("user2", printCmd.Parameters["name"]);

        Assert.Equal("/user-manager/user/remove", removeCmd.Path);
        Assert.Equal("*2B", removeCmd.Parameters["numbers"]);
    }

    [Fact]
    public void HotspotCommandProvider_BuildsCorrectHotspotCommands()
    {
        // Arrange
        var provider = new HotspotCommandProvider();

        // Act
        var addCmd = provider.BuildUserAddCommand("user3", "pass789", "admin");
        var printCmd = provider.BuildUserPrintCommand("user3");
        var removeCmd = provider.BuildUserRemoveCommand("*3C");

        // Assert
        Assert.Equal(RouterSystemType.Hotspot, provider.SystemType);
        Assert.Equal("/ip/hotspot/user/add", addCmd.Path);
        Assert.Equal("user3", addCmd.Parameters["name"]);
        Assert.Equal("pass789", addCmd.Parameters["password"]);

        Assert.Equal("/ip/hotspot/user/print", printCmd.Path);
        Assert.Equal("user3", printCmd.Parameters["name"]);

        Assert.Equal("/ip/hotspot/user/remove", removeCmd.Path);
        Assert.Equal("*3C", removeCmd.Parameters["numbers"]);
    }

    [Theory]
    [InlineData("UMv7", RouterSystemType.UserManagerV7)]
    [InlineData("UMv6", RouterSystemType.UserManagerV6)]
    [InlineData("Hotspot", RouterSystemType.Hotspot)]
    public async Task CommandProviderFactory_ReturnsCorrectProvider(string capType, RouterSystemType expectedType)
    {
        // Arrange
        var mockCap = new Mock<IRouterCapabilityService>();
        mockCap.Setup(c => c.GetProfileSystemTypeAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(capType);

        IMikroTikCommandProvider[] providers = new IMikroTikCommandProvider[]
        {
            new RouterOsV6CommandProvider(),
            new RouterOsV7CommandProvider(),
            new HotspotCommandProvider()
        };

        var factory = new MikroTikCommandProviderFactory(mockCap.Object, providers);

        // Act
        var resultProvider = await factory.GetProviderAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(resultProvider);
        Assert.Equal(expectedType, resultProvider.SystemType);
    }
}
