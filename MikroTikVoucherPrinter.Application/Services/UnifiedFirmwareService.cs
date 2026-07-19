using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MikroTikVoucherPrinter.Application.Services;

public class UnifiedFirmwareService : IUnifiedFirmwareService
{
    private readonly IEnumerable<IDeviceFirmwareProvider> _providers;
    private readonly IUnifiedBackupService _backupService;
    private readonly IFileTransferService _fileTransferService;
    private readonly IReconnectStrategy _reconnectStrategy;
    private readonly ILogger<UnifiedFirmwareService> _logger;

    public UnifiedFirmwareService(
        IEnumerable<IDeviceFirmwareProvider> providers,
        IUnifiedBackupService backupService,
        IFileTransferService fileTransferService,
        IReconnectStrategy reconnectStrategy,
        ILogger<UnifiedFirmwareService> logger)
    {
        _providers = providers;
        _backupService = backupService;
        _fileTransferService = fileTransferService;
        _reconnectStrategy = reconnectStrategy;
        _logger = logger;
    }

    private IDeviceFirmwareProvider GetProvider(IDevice device)
    {
        var provider = _providers.FirstOrDefault(p => p.CanHandle(device));
        if (provider == null)
            throw new NotSupportedException($"No firmware provider found for device vendor: {device.Vendor}");
        
        return provider;
    }

    public async Task<Result<string>> GetCurrentVersionAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = GetProvider(device);
            return await provider.GetCurrentVersionAsync(device, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current version for device {DeviceId}", device.Id);
            return Result<string>.Failure(ex.Message, ErrorType.Unexpected);
        }
    }

    public async Task<Result<FirmwareCompatibilityResult>> ValidateFirmwareAsync(IDevice device, FirmwareImage image, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = GetProvider(device);
            return await provider.ValidateFirmwareAsync(device, image, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate firmware for device {DeviceId}", device.Id);
            return Result<FirmwareCompatibilityResult>.Failure(ex.Message, ErrorType.Unexpected);
        }
    }

    public async Task<Result<FirmwareUpgradeResult>> UpgradeFirmwareAsync(IDevice device, FirmwareImage image, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = GetProvider(device);
            
            var prevResult = await provider.GetCurrentVersionAsync(device, cancellationToken);
            string? prevVersion = prevResult.IsSuccess ? prevResult.Value : null;

            // Step 1: Compatibility Check & Validation
            _logger.LogInformation("Checking compatibility and validating firmware {FirmwareName} for device {DeviceId}", image.Name, device.Id);
            var validationResult = await provider.ValidateFirmwareAsync(device, image, cancellationToken);
            if (!validationResult.IsSuccess || !validationResult.Value!.IsCompatible)
            {
                return Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult
                {
                    Success = false,
                    PreviousVersion = prevVersion,
                    Error = validationResult.Value?.Error ?? validationResult.ErrorMessage ?? "Firmware compatibility check failed."
                });
            }

            // Step 2: Backup before upgrade
            _logger.LogInformation("Taking backup before firmware upgrade for device {DeviceId}", device.Id);
            var backupResult = await _backupService.CreateBackupAsync(device, BackupType.Firmware, cancellationToken);
            if (!backupResult.IsSuccess)
            {
                _logger.LogWarning("Failed to create pre-upgrade backup for device {DeviceId}. Proceeding anyway...", device.Id);
            }

            // Step 3: Upload
            _logger.LogInformation("Uploading firmware {FirmwareName} to device {DeviceId}", image.Name, device.Id);
            string remotePath = provider.GetRemoteUploadPath(device, image);
            var uploadResult = await _fileTransferService.UploadAsync(device, image.FilePath, remotePath, cancellationToken);
            if (!uploadResult.IsSuccess)
            {
                return Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult
                {
                    Success = false,
                    PreviousVersion = prevVersion,
                    Error = $"Upload failed: {uploadResult.ErrorMessage}"
                });
            }

            // Step 4: Upgrade Execution
            _logger.LogInformation("Executing firmware upgrade command for device {DeviceId}", device.Id);
            var upgradeResult = await provider.UpgradeAsync(device, image, cancellationToken);
            
            if (!upgradeResult.IsSuccess || !upgradeResult.Value!.Success)
            {
                _logger.LogError("Firmware upgrade execution failed for device {DeviceId}: {Error}", 
                    device.Id, upgradeResult.Value?.Error ?? upgradeResult.ErrorMessage);
                return upgradeResult;
            }

            // Step 5: Reconnect
            _logger.LogInformation("Waiting for device {DeviceId} to reconnect after upgrade...", device.Id);
            bool reconnected = await _reconnectStrategy.WaitForReconnectAsync(device, TimeSpan.FromMinutes(5), cancellationToken);
            if (!reconnected)
            {
                return Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult
                {
                    Success = false,
                    PreviousVersion = prevVersion,
                    Error = "Device failed to reconnect after upgrade."
                });
            }

            // Step 6: Verify
            var newVersionResult = await provider.GetCurrentVersionAsync(device, cancellationToken);
            string? newVersion = newVersionResult.IsSuccess ? newVersionResult.Value : null;

            _logger.LogInformation("Successfully upgraded firmware for device {DeviceId} from {OldVersion} to {NewVersion}", 
                device.Id, prevVersion, newVersion);

            return Result<FirmwareUpgradeResult>.Success(new FirmwareUpgradeResult
            {
                Success = true,
                PreviousVersion = prevVersion,
                NewVersion = newVersion
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during firmware upgrade for device {DeviceId}", device.Id);
            return Result<FirmwareUpgradeResult>.Failure(ex.Message, ErrorType.Unexpected);
        }
    }
}
