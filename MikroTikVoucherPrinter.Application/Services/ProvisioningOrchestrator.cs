using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Application.Services;

public class ProvisioningOrchestrator : IProvisioningOrchestrator
{
    private readonly ITemplateResolutionService _resolutionService;
    private readonly IUnifiedConfigurationService _configurationService;

    public ProvisioningOrchestrator(
        ITemplateResolutionService resolutionService,
        IUnifiedConfigurationService configurationService)
    {
        _resolutionService = resolutionService;
        _configurationService = configurationService;
    }

    public async Task<Result<DeviceProvisioningResult>> ProvisionDeviceAsync(
        IDevice device,
        ProvisioningTemplate template,
        IReadOnlyDictionary<string, string>? customVariables = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Resolve Template
            var resolutionResult = await _resolutionService.ResolveTemplateAsync(
                template,
                device,
                customVariables?.ToDictionary(k => k.Key, v => v.Value),
                cancellationToken);

            if (!resolutionResult.IsSuccess)
            {
                return Result<DeviceProvisioningResult>.Success(new DeviceProvisioningResult
                {
                    TargetDevice = device,
                    IsSuccess = false,
                    ErrorMessage = $"Template resolution failed: {resolutionResult.ErrorMessage}"
                });
            }

            var resolvedConfiguration = resolutionResult.Value;

            // 2. Apply Configuration (with auto-rollback if needed)
            var applyResult = await _configurationService.ApplyConfigurationAsync(
                device,
                resolvedConfiguration,
                cancellationToken);

            stopwatch.Stop();

            return Result<DeviceProvisioningResult>.Success(new DeviceProvisioningResult
            {
                TargetDevice = device,
                IsSuccess = applyResult.IsSuccess,
                ErrorMessage = applyResult.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Result<DeviceProvisioningResult>.Success(new DeviceProvisioningResult
            {
                TargetDevice = device,
                IsSuccess = false,
                ErrorMessage = $"Unexpected error during provisioning: {ex.Message}"
            });
        }
    }
}
