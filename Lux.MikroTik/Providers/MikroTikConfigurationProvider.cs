using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.MikroTik.Providers;

public class MikroTikConfigurationProvider : IDeviceConfigurationProvider
{
    private readonly IRouterOsProvider _provider;
    private readonly ILogger<MikroTikConfigurationProvider> _logger;

    public MikroTikConfigurationProvider(IRouterOsProvider provider, ILogger<MikroTikConfigurationProvider> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public bool CanHandle(IDevice device)
    {
        return device.GetType().Name == "NetworkDevice" && 
               ((dynamic)device).Vendor.ToString() == "MikroTik";
    }

    public Task<Result<ConfigurationValidationResult>> ValidateConfigurationAsync(DeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var result = new ConfigurationValidationResult();

        if (configuration == null)
        {
            result.Errors.Add("Configuration is null.");
            return Task.FromResult(Result<ConfigurationValidationResult>.Success(result));
        }

        foreach (var section in configuration.Sections)
        {
            if (section.Type == "IP Addresses" && section.Settings.ContainsKey("address"))
            {
                var address = section.Settings["address"];
                if (string.IsNullOrWhiteSpace(address) || !address.Contains("/"))
                {
                    result.Errors.Add($"IP Address '{address}' must be in CIDR notation (e.g., 192.168.1.1/24).");
                }
            }
        }

        return Task.FromResult(Result<ConfigurationValidationResult>.Success(result));
    }

    public async Task<Result<DeviceConfiguration>> ExportConfigurationAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        var netDevice = device as NetworkDevice;
        if (netDevice == null) return Result<DeviceConfiguration>.Failure("Invalid device type", ErrorType.Unexpected);

        try
        {
            if (!_provider.IsConnected)
            {
                return Result<DeviceConfiguration>.Failure("Provider is not connected", ErrorType.ExternalService);
            }

            var deviceConfig = new DeviceConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"MikroTik_Config_{DateTime.UtcNow:yyyyMMddHHmmss}",
                Version = "1.0",
                CreatedAt = DateTime.UtcNow,
                Metadata = "{\"Source\": \"MikroTik Export\"}"
            };

            var commandsToExport = new Dictionary<string, string>
            {
                { "Identity", "/system/identity/print" },
                { "Interfaces", "/interface/print" },
                { "IP Addresses", "/ip/address/print" },
                { "DNS", "/ip/dns/print" }
            };

            foreach (var cmd in commandsToExport)
            {
                var response = await _provider.ExecuteAsync(new MikroTikCommand { Command = cmd.Value });
                if (response.IsSuccess && response.Value.RawData != null)
                {
                    foreach (var row in response.Value.RawData)
                    {
                        var section = new ConfigurationSection
                        {
                            Name = $"{cmd.Key}_{(row.ContainsKey(".id") ? row[".id"] : Guid.NewGuid().ToString("N"))}",
                            Type = cmd.Key,
                            Settings = new Dictionary<string, string>()
                        };

                        foreach (var kvp in row)
                        {
                            if (!kvp.Key.StartsWith("."))
                            {
                                section.Settings[kvp.Key] = kvp.Value;
                            }
                        }

                        deviceConfig.Sections.Add(section);
                    }
                }
            }

            return Result<DeviceConfiguration>.Success(deviceConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configuration for {Host}", netDevice.IpAddress);
            return Result<DeviceConfiguration>.Failure("Failed to export configuration", ErrorType.ExternalService, ex);
        }
    }

    public async Task<Result> ApplyConfigurationAsync(IDevice device, DeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var netDevice = device as NetworkDevice;
        if (netDevice == null) return Result.Failure("Invalid device type", ErrorType.Unexpected);

        var validationResult = await ValidateConfigurationAsync(configuration, cancellationToken);
        if (!validationResult.IsSuccess || !validationResult.Value.IsValid)
        {
            return Result.Failure("Configuration validation failed: " + string.Join(", ", validationResult.Value?.Errors ?? new List<string>()), ErrorType.Validation);
        }

        try
        {
            if (!_provider.IsConnected)
            {
                return Result.Failure("Provider is not connected", ErrorType.ExternalService);
            }

            foreach (var section in configuration.Sections)
            {
                var mikrotikCmd = new MikroTikCommand();
                if (section.Type == "Identity")
                {
                    mikrotikCmd.Command = "/system/identity/set";
                }
                else if (section.Type == "IP Addresses")
                {
                    mikrotikCmd.Command = "/ip/address/add";
                }
                else if (section.Type == "DNS")
                {
                    mikrotikCmd.Command = "/ip/dns/set";
                }
                else
                {
                    continue; // Skip interfaces or other unhandled types for simple apply
                }

                foreach (var kvp in section.Settings)
                {
                    mikrotikCmd.Parameters[kvp.Key] = kvp.Value;
                }

                try
                {
                    await _provider.ExecuteAsync(mikrotikCmd);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to apply section {SectionName}", section.Name);
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration for {Host}", netDevice.IpAddress);
            return Result.Failure($"Failed to apply configuration: {ex.Message}", ErrorType.ExternalService, ex);
        }
    }
}
