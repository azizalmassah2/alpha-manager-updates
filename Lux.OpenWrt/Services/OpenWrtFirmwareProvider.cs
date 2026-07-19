using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class OpenWrtFirmwareProvider : IDeviceFirmwareProvider
{
    private readonly ILogger<OpenWrtFirmwareProvider> _logger;

    public OpenWrtFirmwareProvider(ILogger<OpenWrtFirmwareProvider> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(IDevice device) => device.Vendor == DeviceVendor.OpenWrt;

    public Task<Result<string>> GetCurrentVersionAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<string>.Success(device.FirmwareVersion ?? "22.03.5"));
    }

    public string GetRemoteUploadPath(IDevice device, FirmwareImage image)
    {
        return $"/tmp/{image.Name}";
    }

    public Task<Result<FirmwareCompatibilityResult>> ValidateFirmwareAsync(IDevice device, FirmwareImage image, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock validation for OpenWrt firmware {ImageName}", image.Name);
        
        var result = new FirmwareCompatibilityResult
        {
            IsCompatible = true,
            CurrentModel = device.Model,
            FirmwareModel = image.BoardName ?? image.Architecture
        };

        return Task.FromResult(Result<FirmwareCompatibilityResult>.Success(result));
    }

    public async Task<Result<FirmwareUpgradeResult>> UpgradeAsync(IDevice device, FirmwareImage image, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing sysupgrade...");
            await Task.Delay(500, cancellationToken); // Mock sysupgrade

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
