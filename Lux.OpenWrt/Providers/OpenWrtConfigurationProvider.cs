using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.OpenWrt.Providers;

public class OpenWrtConfigurationProvider : IDeviceConfigurationProvider
{
    private readonly IUciService _uci;
    private readonly IUbusClient _ubusClient;
    private readonly ILogger<OpenWrtConfigurationProvider> _logger;

    public OpenWrtConfigurationProvider(IUciService uci, IUbusClient ubusClient, ILogger<OpenWrtConfigurationProvider> logger)
    {
        _uci = uci;
        _ubusClient = ubusClient;
        _logger = logger;
    }

    public bool CanHandle(IDevice device)
    {
        return device.GetType().Name == "NetworkDevice" && 
               ((dynamic)device).Vendor.ToString() == "OpenWrt";
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
            if (string.IsNullOrWhiteSpace(section.Name))
            {
                result.Errors.Add($"A section is missing a name.");
            }
            if (section.Type == "wireless" && section.Settings.ContainsKey("ssid"))
            {
                var ssid = section.Settings["ssid"];
                if (string.IsNullOrWhiteSpace(ssid))
                {
                    result.Errors.Add($"Wireless SSID cannot be empty.");
                }
                if (ssid != null && ssid.Contains(" ") && !ssid.StartsWith("\""))
                {
                    result.Warnings.Add($"Wireless SSID '{ssid}' contains spaces. Make sure it is handled correctly.");
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
            var session = await _ubusClient.LoginAsync(netDevice.IpAddress, netDevice.Username ?? "root", netDevice.Password ?? "root", cancellationToken);
            
            var deviceConfig = new DeviceConfiguration
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"OpenWrt_Config_{DateTime.UtcNow:yyyyMMddHHmmss}",
                Version = "1.0",
                CreatedAt = DateTime.UtcNow,
                Metadata = "{\"Source\": \"OpenWrt Export\"}"
            };

            var configsToBackup = new[] { "system", "network", "wireless" };
            foreach (var config in configsToBackup)
            {
                try
                {
                    var dict = await _uci.GetConfigDictAsync(netDevice.IpAddress, session, config, cancellationToken);
                    foreach (var kvp in dict)
                    {
                        var sectionName = kvp.Key;
                        if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Object)
                        {
                            var settings = new Dictionary<string, string>();
                            string type = "";
                            foreach (var property in element.EnumerateObject())
                            {
                                if (property.Name == ".type")
                                {
                                    type = property.Value.GetString() ?? "";
                                }
                                else if (!property.Name.StartsWith("."))
                                {
                                    settings[property.Name] = property.Value.ToString() ?? "";
                                }
                            }

                            deviceConfig.Sections.Add(new ConfigurationSection
                            {
                                Name = $"{config}.{sectionName}",
                                Type = type,
                                Settings = settings
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to export config section {Config} for {Host}", config, netDevice.IpAddress);
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
            var session = await _ubusClient.LoginAsync(netDevice.IpAddress, netDevice.Username ?? "root", netDevice.Password ?? "root", cancellationToken);

            var affectedConfigs = new HashSet<string>();

            foreach (var section in configuration.Sections)
            {
                var parts = section.Name.Split('.');
                if (parts.Length < 2) continue;
                
                var config = parts[0];
                var sectionName = parts[1];
                
                affectedConfigs.Add(config);

                var objectValues = new Dictionary<string, object>();
                foreach (var kvp in section.Settings)
                {
                    objectValues[kvp.Key] = kvp.Value;
                }

                try
                {
                    await _uci.SetAsync(netDevice.IpAddress, session, config, sectionName, objectValues, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set UCI config for {Config}.{Section}", config, sectionName);
                }
            }

            foreach (var config in affectedConfigs)
            {
                await _uci.CommitAsync(netDevice.IpAddress, session, config, cancellationToken);
            }

            await _uci.ApplyAsync(netDevice.IpAddress, session, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration for {Host}", netDevice.IpAddress);
            return Result.Failure($"Failed to apply configuration: {ex.Message}", ErrorType.ExternalService, ex);
        }
    }
}
