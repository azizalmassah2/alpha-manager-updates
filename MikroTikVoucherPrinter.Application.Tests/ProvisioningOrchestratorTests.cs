using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Services;
using MikroTikVoucherPrinter.Domain.Entities;
using Moq;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class ProvisioningOrchestratorTests
{
    private readonly Mock<ITemplateResolutionService> _resolutionMock;
    private readonly Mock<IUnifiedConfigurationService> _configMock;
    private readonly ProvisioningOrchestrator _orchestrator;

    public ProvisioningOrchestratorTests()
    {
        _resolutionMock = new Mock<ITemplateResolutionService>();
        _configMock = new Mock<IUnifiedConfigurationService>();
        _orchestrator = new ProvisioningOrchestrator(_resolutionMock.Object, _configMock.Object);
    }

    [Fact]
    public async Task ProvisionDeviceAsync_Successful_ReturnsSuccessResult()
    {
        // Arrange
        var device = new NetworkDevice { Name = "D1" };
        var template = new ProvisioningTemplate();
        var resolvedConfig = new DeviceConfiguration();

        _resolutionMock.Setup(r => r.ResolveTemplateAsync(template, device, null, default))
            .ReturnsAsync(Result<DeviceConfiguration>.Success(resolvedConfig));

        _configMock.Setup(c => c.ApplyConfigurationAsync(device, resolvedConfig, default))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _orchestrator.ProvisionDeviceAsync(device, template);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsSuccess);
        Assert.Equal(device, result.Value.TargetDevice);

        _configMock.Verify(c => c.ApplyConfigurationAsync(device, resolvedConfig, default), Times.Once);
    }

    [Fact]
    public async Task ProvisionDeviceAsync_ApplyFails_ReturnsFailureResult()
    {
        // Arrange
        var device = new NetworkDevice { Name = "D2" };
        var template = new ProvisioningTemplate();
        var resolvedConfig = new DeviceConfiguration();

        _resolutionMock.Setup(r => r.ResolveTemplateAsync(template, device, null, default))
            .ReturnsAsync(Result<DeviceConfiguration>.Success(resolvedConfig));

        _configMock.Setup(c => c.ApplyConfigurationAsync(device, resolvedConfig, default))
            .ReturnsAsync(Result.Failure("Apply error", ErrorType.ExternalService));

        // Act
        var result = await _orchestrator.ProvisionDeviceAsync(device, template);

        // Assert
        Assert.True(result.IsSuccess); // The method returns a success wrapping a failure result
        Assert.False(result.Value.IsSuccess);
        Assert.Contains("Apply error", result.Value.ErrorMessage);
    }
}
