using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// خدمة الإنشاء الجديدة المستقلة لإدارة مستخدمي RouterOS.
/// لا ترث ولا تعتمد على LegacyMikroTikIntegrationService.
/// تستخدم IMikroTikCommandProviderFactory حصراً لتحديد الأوامر المناسبة للإصدار.
/// </summary>
public sealed class MikroTikProvisioningService
{
    private readonly IMikroTikCommandProviderFactory _providerFactory;
    private readonly IMikroTikCommandExecutor _commandExecutor;
    private readonly ILogger<MikroTikProvisioningService> _logger;

    public MikroTikProvisioningService(
        IMikroTikCommandProviderFactory providerFactory,
        IMikroTikCommandExecutor commandExecutor,
        ILogger<MikroTikProvisioningService> logger)
    {
        _providerFactory = providerFactory;
        _commandExecutor = commandExecutor;
        _logger = logger;
    }

    /// <summary>
    /// يضيف مستخدماً واحداً ويعيّن له الباقة المطلوبة.
    /// الأوامر المُستخدمة تعتمد تلقائياً على إصدار الراوتر المتصل.
    /// </summary>
    public async Task<Result<MikroTikUserResult>> CreateUserAsync(
        string username, string? password, string profileName, string adminUser,
        CancellationToken ct = default)
    {
        try
        {
            var provider = await _providerFactory.GetProviderAsync(ct);
            _logger.LogDebug("Provisioning: Creating user {U} via {Type}", username, provider.SystemType);

            // 1. إضافة المستخدم
            var addCmd = provider.BuildUserAddCommand(username, password, adminUser);
            var addResult = await ExecuteAsync(addCmd, ct);
            if (!addResult.IsSuccess)
                return Result<MikroTikUserResult>.Failure(addResult.ErrorMessage ?? "Failed to add user", ErrorType.ExternalService);

            // 2. تعيين الباقة — فقط لـ UserManager (V6 أو V7)
            if (provider.SystemType != RouterSystemType.Hotspot)
            {
                var profileCmd = provider.BuildAssignProfileCommand(username, profileName, adminUser);
                var profileResult = await ExecuteAsync(profileCmd, ct);
                if (!profileResult.IsSuccess)
                    _logger.LogWarning("Provisioning: Profile assignment failed for {U}: {Msg}", username, profileResult.ErrorMessage);
            }

            _logger.LogInformation("Provisioning: User {U} created successfully via {Type}", username, provider.SystemType);
            return Result<MikroTikUserResult>.Success(new MikroTikUserResult
            {
                Username = username,
                WasAlreadyPresent = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning: Unexpected error creating user {U}", username);
            return Result<MikroTikUserResult>.Failure($"Unexpected error: {ex.Message}", ErrorType.ExternalService);
        }
    }

    /// <summary>
    /// يحذف مستخدماً واحداً بمعرفه الداخلي.
    /// </summary>
    public async Task<Result> DeleteUserAsync(string internalId, CancellationToken ct = default)
    {
        try
        {
            var provider = await _providerFactory.GetProviderAsync(ct);
            var cmd = provider.BuildUserRemoveCommand(internalId);
            var result = await ExecuteAsync(cmd, ct);

            if (!result.IsSuccess)
                return Result.Failure(result.ErrorMessage ?? "Failed to remove user", ErrorType.ExternalService);

            _logger.LogInformation("Provisioning: User {Id} deleted via {Type}", internalId, provider.SystemType);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning: Unexpected error deleting user {Id}", internalId);
            return Result.Failure($"Unexpected error: {ex.Message}", ErrorType.ExternalService);
        }
    }

    /// <summary>
    /// يحذف مجموعة مستخدمين بمعرفاتهم الداخلية (Batch).
    /// </summary>
    public async Task<Result> DeleteUsersBulkAsync(IEnumerable<string> internalIds, CancellationToken ct = default)
    {
        try
        {
            var idList = internalIds.ToList();
            if (idList.Count == 0) return Result.Success();

            var provider = await _providerFactory.GetProviderAsync(ct);
            var cmd = provider.BuildUserBulkRemoveCommand(idList);
            var result = await ExecuteAsync(cmd, ct);

            if (!result.IsSuccess)
                return Result.Failure(result.ErrorMessage ?? "Failed to bulk remove users", ErrorType.ExternalService);

            _logger.LogInformation("Provisioning: Bulk deleted {Count} users via {Type}", idList.Count, provider.SystemType);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning: Unexpected error during bulk delete");
            return Result.Failure($"Unexpected error: {ex.Message}", ErrorType.ExternalService);
        }
    }

    // ── Private Helper ──────────────────────────────────────────────────────────

    private async Task<Result> ExecuteAsync(RouterCommand routerCommand, CancellationToken ct)
    {
        try
        {
            var command = new MikroTikCommand
            {
                Command = routerCommand.Path,
                Parameters = routerCommand.Parameters.ToDictionary(k => k.Key, v => v.Value)
            };

            var response = await _commandExecutor.ExecuteAsync(command, ct);

            if (!response.Success)
                return Result.Failure(response.Message ?? "Router returned an error", ErrorType.ExternalService);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Provisioning: Command {Cmd} failed: {Msg}", routerCommand.Path, ex.Message);
            return Result.Failure(ex.Message, ErrorType.ExternalService);
        }
    }
}
