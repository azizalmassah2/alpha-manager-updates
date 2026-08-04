using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Interfaces;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class FeatureFlaggedMikroTikIntegrationService : IMikroTikIntegrationService
{
    private readonly IMikroTikIntegrationService _legacyService;
    private readonly IMikroTikVoucherManager _modernService;
    private readonly ISettingsService _settingsService;

    public FeatureFlaggedMikroTikIntegrationService(
        LegacyMikroTikIntegrationService legacyService,
        IMikroTikVoucherManager modernService,
        ISettingsService settingsService)
    {
        _legacyService = legacyService;
        _modernService = modernService;
        _settingsService = settingsService;
    }

    private bool UseModern => _settingsService.Get("Connectivity.UseModernArchitecture", false);

    public async Task<Result<MikroTikUserResult>> CreateUserAsync(string username, string? password, string profileName, CancellationToken cancellationToken = default)
    {
        if (UseModern)
        {
            return await _modernService.CreateUserAsync(username, password, profileName, cancellationToken);
        }
        else
        {
            return await _legacyService.CreateUserAsync(username, password, profileName, cancellationToken);
        }
    }

    public async Task<Dictionary<string, Result<MikroTikUserResult>>> CreateUsersBulkAsync(
        IEnumerable<(string username, string? password, string profileName)> users,
        IProgress<(int success, int failed, int total)>? progress = null,
        int initialSuccess = 0,
        int initialFailed = 0,
        CancellationToken cancellationToken = default)
    {
        if (UseModern)
        {
            return await _modernService.CreateUsersBulkAsync(users, progress, initialSuccess, initialFailed, cancellationToken);
        }
        else
        {
            return await _legacyService.CreateUsersBulkAsync(users, progress, initialSuccess, initialFailed, cancellationToken);
        }
    }

    public async Task<Result> DeleteUserAsync(string username, string? externalId = null, CancellationToken cancellationToken = default)
    {
        if (UseModern)
        {
            return await _modernService.DeleteUserAsync(username, externalId, cancellationToken);
        }
        else
        {
            return await _legacyService.DeleteUserAsync(username, externalId, cancellationToken);
        }
    }

    public async Task<Dictionary<string, Result>> DeleteUsersBulkAsync(
        IEnumerable<(string username, string? externalId)> users,
        IProgress<(int success, int failed, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (UseModern)
        {
            return await _modernService.DeleteUsersBulkAsync(users, progress, cancellationToken);
        }
        else
        {
            return await _legacyService.DeleteUsersBulkAsync(users, progress, cancellationToken);
        }
    }

    public void ResetCircuitBreaker()
    {
        _legacyService.ResetCircuitBreaker();
    }
}

