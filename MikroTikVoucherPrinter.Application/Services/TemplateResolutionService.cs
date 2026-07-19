using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Application.Services;

public class TemplateResolutionService : ITemplateResolutionService
{
    public Task<Result<DeviceConfiguration>> ResolveTemplateAsync(
        ProvisioningTemplate template, 
        IDevice device, 
        IDictionary<string, string>? additionalVariables = null, 
        CancellationToken cancellationToken = default)
    {
        if (template == null) return Task.FromResult(Result<DeviceConfiguration>.Failure("Template is null", ErrorType.Validation));
        if (template.BaseConfiguration == null) return Task.FromResult(Result<DeviceConfiguration>.Failure("Template base configuration is null", ErrorType.Validation));
        if (device == null) return Task.FromResult(Result<DeviceConfiguration>.Failure("Device is null", ErrorType.Validation));

        // 1. Deep clone the BaseConfiguration to avoid modifying the template reference
        var json = JsonSerializer.Serialize(template.BaseConfiguration);

        // 2. Build variables dictionary
        var variables = new Dictionary<string, string>
        {
            { "DeviceName", device.Name ?? string.Empty },
            { "IpAddress", device.IpAddress ?? string.Empty },
            { "MacAddress", device.MacAddress ?? string.Empty }
        };

        if (additionalVariables != null)
        {
            foreach (var kvp in additionalVariables)
            {
                variables[kvp.Key] = kvp.Value;
            }
        }

        // 3. Replace variables in the JSON string
        var resolvedJson = ReplaceVariables(json, variables);

        // 4. Deserialize back to DeviceConfiguration
        try
        {
            var resolvedConfig = JsonSerializer.Deserialize<DeviceConfiguration>(resolvedJson);
            if (resolvedConfig == null)
            {
                return Task.FromResult(Result<DeviceConfiguration>.Failure("Failed to deserialize resolved configuration.", ErrorType.Unexpected));
            }

            return Task.FromResult(Result<DeviceConfiguration>.Success(resolvedConfig));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(Result<DeviceConfiguration>.Failure($"Error parsing resolved configuration: {ex.Message}", ErrorType.Unexpected));
        }
    }

    private string ReplaceVariables(string input, IDictionary<string, string> variables)
    {
        var result = input;
        foreach (var variable in variables)
        {
            // Replace {{VariableName}} with the actual value. We use string.Replace for performance on simple exact matches.
            result = result.Replace("{{" + variable.Key + "}}", variable.Value);
        }
        
        // Optionally, we could clean up any un-resolved variables or leave them. 
        // For now, we leave them in case they are intentional or handled later.
        
        return result;
    }
}
