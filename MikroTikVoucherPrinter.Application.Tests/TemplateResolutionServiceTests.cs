using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Services;
using MikroTikVoucherPrinter.Domain.Entities;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class TemplateResolutionServiceTests
{
    private readonly TemplateResolutionService _service;

    public TemplateResolutionServiceTests()
    {
        _service = new TemplateResolutionService();
    }

    [Fact]
    public async Task ResolveTemplateAsync_ReplacesVariablesCorrectly()
    {
        // Arrange
        var baseConfig = new DeviceConfiguration
        {
            Metadata = "TargetIP: {{IpAddress}}, Name: {{DeviceName}}",
            Sections = new List<ConfigurationSection>
            {
                new ConfigurationSection
                {
                    Name = "network",
                    Settings = new Dictionary<string, string>
                    {
                        { "ipaddr", "{{CustomIp}}" },
                        { "hostname", "{{DeviceName}}" }
                    }
                }
            }
        };

        var template = new ProvisioningTemplate { BaseConfiguration = baseConfig };
        
        var device = new NetworkDevice
        {
            Name = "Router-1",
            IpAddress = "192.168.1.1",
            MacAddress = "00:11:22:33:44:55"
        };

        var customVars = new Dictionary<string, string> { { "CustomIp", "10.0.0.1" } };

        // Act
        var result = await _service.ResolveTemplateAsync(template, device, customVars);

        // Assert
        Assert.True(result.IsSuccess);
        
        var resolvedConfig = result.Value;
        
        // Metadata validation
        Assert.Equal("TargetIP: 192.168.1.1, Name: Router-1", resolvedConfig.Metadata);
        
        // Section properties validation
        Assert.Single(resolvedConfig.Sections);
        var section = resolvedConfig.Sections[0];
        Assert.Equal("10.0.0.1", section.Settings["ipaddr"]);
        Assert.Equal("Router-1", section.Settings["hostname"]);
    }
}
