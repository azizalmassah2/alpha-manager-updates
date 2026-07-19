using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Lux.MikroTik.Models;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.MikroTik.Providers;

public class MikroTikFirmwareProvider : IDeviceFirmwareProvider
{
    private readonly IRouterOsApiClient _apiClient;
    private readonly ILogger<MikroTikFirmwareProvider> _logger;

    public MikroTikFirmwareProvider(
        IRouterOsApiClient apiClient,
        ILogger<MikroTikFirmwareProvider> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public bool CanHandle(IDevice device) => device.Vendor == DeviceVendor.MikroTik;

    public async Task<Result<string>> GetCurrentVersionAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        try
        {
            var netDevice = device as NetworkDevice;
            if (netDevice == null) return Result<string>.Failure("Invalid device type", ErrorType.Unexpected);

            var result = await _apiClient.ExecuteAsync("/system/resource/print");

            var item = result.FirstOrDefault();
            if (item != null && item.TryGetValue("version", out string? version))
            {
                return Result<string>.Success(version.Split(' ')[0]);
            }

            return Result<string>.Failure("Failed to extract version", ErrorType.Unexpected);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to get current version: {ex.Message}", ErrorType.Unexpected);
        }
    }

    public string GetRemoteUploadPath(IDevice device, FirmwareImage image)
    {
        return image.Name;
    }

    public Task<Result<FirmwareCompatibilityResult>> ValidateFirmwareAsync(IDevice device, FirmwareImage image, CancellationToken cancellationToken = default)
    {
        var result = new FirmwareCompatibilityResult
        {
            IsCompatible = image.Name.EndsWith(".npk", StringComparison.OrdinalIgnoreCase),
            CurrentModel = device.Model,
            FirmwareModel = image.Architecture
        };

        return Task.FromResult(Result<FirmwareCompatibilityResult>.Success(result));
    }

    public async Task<Result<FirmwareUpgradeResult>> UpgradeAsync(IDevice device, FirmwareImage image, CancellationToken cancellationToken = default)
    {
        try
        {
            var netDevice = device as NetworkDevice;
            if (netDevice == null) 
                return Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult { Success = false, Error = "Invalid device" });

            _logger.LogInformation("Rebooting device to install update...");
            try { await _apiClient.ExecuteAsync("/system/reboot"); } catch { }

            return Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult
            {
                Success = true
            });
        }
        catch (Exception ex)
        {
            return Result<FirmwareUpgradeResult>.Failure($"Upgrade failed: {ex.Message}", ErrorType.Unexpected);
        }
    }
}
