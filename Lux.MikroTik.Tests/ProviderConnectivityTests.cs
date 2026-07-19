using System;
using System.Threading.Tasks;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using Lux.MikroTik.Providers;
using Lux.Platform.Abstractions.Common;
using Moq;
using Xunit;

namespace Lux.MikroTik.Tests;

public class ProviderConnectivityTests
{
    [Fact]
    public async Task Connection_ConnectAsync_UsesProvider()
    {
        var mockProvider = new Mock<IRouterOsProvider>();
        mockProvider.Setup(p => p.ConnectAsync(It.IsAny<MikroTikConnectionOptions>()))
                    .ReturnsAsync(Result.Success());

        var connection = new MikroTikConnection(mockProvider.Object);
        var options = new MikroTikConnectionOptions();

        await connection.ConnectAsync(options);

        mockProvider.Verify(p => p.ConnectAsync(options), Times.Once);
    }

    [Fact]
    public async Task Executor_ExecuteAsync_UsesProvider()
    {
        var mockProvider = new Mock<IRouterOsProvider>();
        var response = new MikroTikResponse { Success = true };
        mockProvider.Setup(p => p.ExecuteAsync(It.IsAny<MikroTikCommand>()))
                    .ReturnsAsync(Result<MikroTikResponse>.Success(response));

        var executor = new MikroTikCommandExecutor(mockProvider.Object);
        var command = new MikroTikCommand();

        var result = await executor.ExecuteAsync(command);

        Assert.True(result.Success);
        mockProvider.Verify(p => p.ExecuteAsync(command), Times.Once);
    }
}
