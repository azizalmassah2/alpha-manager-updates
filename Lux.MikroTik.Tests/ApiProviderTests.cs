using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Exceptions;
using Lux.MikroTik.Models;
using Lux.MikroTik.Providers;
using Lux.Platform.Abstractions.Common;
using Moq;
using Xunit;

namespace Lux.MikroTik.Tests;

public class ApiProviderTests
{
    [Fact]
    public async Task Provider_ConnectAsync_TranslatesException()
    {
        var mockApiClient = new Mock<IRouterOsApiClient>();
        mockApiClient.Setup(c => c.ConnectAsync(It.IsAny<MikroTikConnectionOptions>()))
                     .ThrowsAsync(new Exception("Tik4net connection error"));

        var provider = new RouterOsApiProvider(mockApiClient.Object);

        await Assert.ThrowsAsync<MikroTikConnectionException>(() => provider.ConnectAsync(new MikroTikConnectionOptions()));
    }

    [Fact]
    public async Task Provider_ExecuteAsync_TranslatesException()
    {
        var mockApiClient = new Mock<IRouterOsApiClient>();
        mockApiClient.Setup(c => c.ConnectAsync(It.IsAny<MikroTikConnectionOptions>())).Returns(Task.CompletedTask);
        mockApiClient.Setup(c => c.ExecuteAsync(It.IsAny<string>()))
                     .ThrowsAsync(new Exception("Tik4net execute error"));

        var provider = new RouterOsApiProvider(mockApiClient.Object);
        await provider.ConnectAsync(new MikroTikConnectionOptions()); // Set connected state

        await Assert.ThrowsAsync<MikroTikCommandException>(() => provider.ExecuteAsync(new MikroTikCommand { Command = "/system/identity/print" }));
    }

    [Fact]
    public async Task Provider_ExecuteAsync_MapsDataCorrectly()
    {
        var mockApiClient = new Mock<IRouterOsApiClient>();
        mockApiClient.Setup(c => c.ConnectAsync(It.IsAny<MikroTikConnectionOptions>())).Returns(Task.CompletedTask);

        var sampleData = new List<IDictionary<string, string>>
        {
            new Dictionary<string, string> { { "name", "MikroTik-Router" } }
        };

        mockApiClient.Setup(c => c.ExecuteAsync(It.IsAny<string>())).ReturnsAsync(sampleData);

        var provider = new RouterOsApiProvider(mockApiClient.Object);
        await provider.ConnectAsync(new MikroTikConnectionOptions());

        var result = await provider.ExecuteAsync(new MikroTikCommand { Command = "/system/identity/print" });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.RawData);
        Assert.Equal("MikroTik-Router", result.Value.RawData[0]["name"]);
    }
}
