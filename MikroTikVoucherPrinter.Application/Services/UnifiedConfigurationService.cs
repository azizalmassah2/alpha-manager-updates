using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Application.Services;

public class UnifiedConfigurationService : IUnifiedConfigurationService
{
    private readonly IEnumerable<IDeviceConfigurationProvider> _providers;
    private readonly IUnifiedBackupService _backupService;

    public UnifiedConfigurationService(IEnumerable<IDeviceConfigurationProvider> providers, IUnifiedBackupService backupService)
    {
        _providers = providers;
        _backupService = backupService;
    }

    private IDeviceConfigurationProvider? GetProvider(IDevice device)
    {
        return _providers.FirstOrDefault(p => p.CanHandle(device));
    }

    public async Task<Result> ApplyConfigurationAsync(IDevice device, DeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration == null)
            return Result.Failure("Configuration cannot be null.", ErrorType.Validation);

        if (configuration.Sections == null || configuration.Sections.Count == 0)
            return Result.Failure("Configuration contains no sections to apply.", ErrorType.Validation);

        var provider = GetProvider(device);
        if (provider == null)
            return Result.Failure("No suitable configuration provider found.", ErrorType.Validation);

        // 1. Generic & Vendor Validation
        var validation = await provider.ValidateConfigurationAsync(configuration, cancellationToken);
        if (!validation.IsSuccess || !validation.Value.IsValid)
        {
            return Result.Failure("Validation failed: " + string.Join(", ", validation.Value?.Errors ?? new List<string>()), ErrorType.Validation);
        }

        // 2. Automatic Backup Before Apply
        var backupResult = await _backupService.CreateBackupAsync(device, BackupType.PreDeploymentRollback, cancellationToken);
        if (!backupResult.IsSuccess)
        {
            return Result.Failure($"Failed to create pre-apply backup: {backupResult.ErrorMessage}", ErrorType.ExternalService);
        }

        // 3. Apply Configuration
        var applyResult = await provider.ApplyConfigurationAsync(device, configuration, cancellationToken);
        if (!applyResult.IsSuccess)
        {
            // 4. Rollback
            var restoreResult = await _backupService.RestoreBackupAsync(device, backupResult.Value, cancellationToken);
            var rollbackMsg = restoreResult.IsSuccess ? "Rollback successful" : "Rollback failed";
            return Result.Failure($"Failed to apply configuration. {rollbackMsg}. Original Error: {applyResult.ErrorMessage}", ErrorType.ExternalService);
        }

        return Result.Success();
    }

    public Task<Result<DeviceConfiguration>> ExportConfigurationAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(device);
        if (provider == null)
            return Task.FromResult(Result<DeviceConfiguration>.Failure("No suitable configuration provider found.", ErrorType.Validation));

        return provider.ExportConfigurationAsync(device, cancellationToken);
    }

    public Task<Result<ConfigurationValidationResult>> ValidateConfigurationAsync(IDevice device, DeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(device);
        if (provider == null)
            return Task.FromResult(Result<ConfigurationValidationResult>.Failure("No suitable configuration provider found.", ErrorType.Validation));

        return provider.ValidateConfigurationAsync(configuration, cancellationToken);
    }
}
