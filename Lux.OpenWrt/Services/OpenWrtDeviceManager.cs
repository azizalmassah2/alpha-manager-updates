using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Domain.Common;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class OpenWrtDeviceManager : IOpenWrtDeviceManager
{
    private readonly IUbusClient _ubusClient;
    private readonly IUciService _uciService;
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly IBackupRestoreService _backupRestoreService;
    private readonly ILogger<OpenWrtDeviceManager> _logger;

    public OpenWrtDeviceManager(
        IUbusClient ubusClient,
        IUciService uciService,
        IDeviceDiscoveryService discoveryService,
        IBackupRestoreService backupRestoreService,
        ILogger<OpenWrtDeviceManager> logger)
    {
        _ubusClient = ubusClient;
        _uciService = uciService;
        _discoveryService = discoveryService;
        _backupRestoreService = backupRestoreService;
        _logger = logger;
    }

    public async Task<Result<NetworkDevice>> DiscoverDeviceAsync(string host, string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("ط¨ط¯ط، ط¹ظ…ظ„ظٹط© ظپط­طµ ط§ظ„ط¬ظ‡ط§ط² {Host} (OpenWrtDeviceManager)...", host);

            // 1. طھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„ ظ„ظ„ط­طµظˆظ„ ط¹ظ„ظ‰ Session
            var (session, acls) = await _ubusClient.LoginWithAclsAsync(host, username, password, cancellationToken);
            
            _logger.LogInformation("طھظ… طھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„ ط¨ظ†ط¬ط§ط­ ط¥ظ„ظ‰ {Host} - Session طھظ… ط¥ظ†ط´ط§ط¤ظ‡.", host);

            // 2. ط§ط³طھط®ط¯ط§ظ… DeviceDiscoveryService ظ„ط§ظƒطھط´ط§ظپ ط§ظ„ط¨ظٹط§ظ†ط§طھ
            var discoveryResult = await _discoveryService.DiscoverDeviceAsync(host, session, cancellationToken);
            
            if (discoveryResult.IsSuccess && discoveryResult.Value != null)
            {
                // ظٹظ…ظƒظ†ظ†ط§ ط¥ط¶ط§ظپط© ظ…ط¹ظ„ظˆظ…ط§طھ ACL ط£ظˆ ط£ظٹ ط¨ظٹط§ظ†ط§طھ ط¥ط¶ط§ظپظٹط© ظپظٹ ط§ظ„ظ€ Metadata ط¥ظ† ط£ط±ط¯ظ†ط§ ظ„ط§ط­ظ‚ط§ظ‹
                _logger.LogInformation("ط§ظƒطھط´ط§ظپ ط§ظ„ط¬ظ‡ط§ط² {Host} طھظ… ط¨ظ†ط¬ط§ط­.", host);
                return Result<NetworkDevice>.Success(discoveryResult.Value);
            }

            _logger.LogWarning("ط§ظƒطھط´ط§ظپ ط§ظ„ط¬ظ‡ط§ط² {Host} ظپط´ظ„ ط£ظˆ ط£ط±ط¬ط¹ ظ†طھظٹط¬ط© ظپط§ط±ط؛ط©.", host);
            return discoveryResult; // ط¥ط±ط¬ط§ط¹ ط§ظ„ظپط´ظ„ ط§ظ„ط£طµظ„ظٹ
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("طھظ… ط¥ظ„ط؛ط§ط، ط£ظˆ ط§ظ†طھظ‡ط§ط، ظ…ظ‡ظ„ط© ظپط­طµ ط§ظ„ط¬ظ‡ط§ط² {Host}.", host);
            return Result<NetworkDevice>.Failure("ط§ظ†طھظ‡طھ ظ…ظ‡ظ„ط© ط§ظ„ط§طھطµط§ظ„ ط£ظˆ طھظ… ط¥ظ„ط؛ط§ط، ط§ظ„ط¹ظ…ظ„ظٹط©", ErrorType.ExternalService, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ظپط´ظ„ ظپظٹ طھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„ ط£ظˆ ظپط­طµ ط§ظ„ط¬ظ‡ط§ط² {Host}: {Message}", host, ex.Message);
            return Result<NetworkDevice>.Failure($"ظپط´ظ„ ظپظٹ ط§ظ„ط§طھطµط§ظ„ ط£ظˆ ط§ظƒطھط´ط§ظپ ط§ظ„ط¬ظ‡ط§ط²: {ex.Message}", ErrorType.Unexpected, ex);
        }
    }

    public async Task<bool> IsReachableAsync(string host, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("ط§ظ„طھط­ظ‚ظ‚ ظ…ظ† ط§طھطµط§ظ„ ط§ظ„ط¬ظ‡ط§ط² {Host} (Ping)...", host);
            using var ping = new Ping();
            
            // Ping.SendPingAsync does not take CancellationToken directly in some .NET versions without extension method,
            // but in .NET 8, it does accept CancellationToken or we can register to it.
            // Wait, we can use task.WaitAsync(cancellationToken) to safely support cancellation.
            
            var reply = await ping.SendPingAsync(host, 2000).WaitAsync(cancellationToken);
            
            var isReachable = reply.Status == IPStatus.Success;
            
            if (isReachable)
                _logger.LogInformation("ط§ظ„ط¬ظ‡ط§ط² {Host} ظٹظ…ظƒظ† ط§ظ„ظˆطµظˆظ„ ط¥ظ„ظٹظ‡.", host);
            else
                _logger.LogWarning("ط§ظ„ط¬ظ‡ط§ط² {Host} ظ„ط§ ظٹط±ط¯ ط¹ظ„ظ‰ ط§ظ„ظ€ Ping (ط§ظ„ط­ط§ظ„ط©: {Status}).", host, reply.Status);
                
            return isReachable;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ط¹ظ…ظ„ظٹط© ط§ظ„ظ€ Ping ظ„ظ„ط¬ظ‡ط§ط² {Host} ط£ظڈظ„ط؛ظٹطھ ط£ظˆ ط§ظ†طھظ‡طھ ط§ظ„ظ…ظ‡ظ„ط©.", host);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ط®ط·ط£ ط£ط«ظ†ط§ط، ظ…ط­ط§ظˆظ„ط© ط§ظ„ظˆطµظˆظ„ ظ„ظ„ط¬ظ‡ط§ط² {Host}: {Message}", host, ex.Message);
            return false;
        }
    }

    public async Task<Result<DeviceBackup>> CreateBackupAsync(string host, string username, string password, BackupType backupType, CancellationToken cancellationToken = default)
    {
        try
        {
            var (session, acls) = await _ubusClient.LoginWithAclsAsync(host, username, password, cancellationToken);
            return await _backupRestoreService.CreateBackupAsync(host, session, host, backupType, "Unknown OpenWrt", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ظپط´ظ„ ط§ظ„ط§طھطµط§ظ„ ظ„ط¥ظ†ط´ط§ط، ط§ظ„ظ†ط³ط®ط© ط§ظ„ط§ط­طھظٹط§ط·ظٹط© ظ„ظ„ط¬ظ‡ط§ط² {Host}", host);
            return Result<DeviceBackup>.Failure($"ظپط´ظ„ ط§ظ„ط§طھطµط§ظ„: {ex.Message}", ErrorType.Unexpected, ex);
        }
    }

    public async Task<Result<bool>> RestoreBackupAsync(string host, string username, string password, string backupFilePath, string expectedChecksum, CancellationToken cancellationToken = default)
    {
        try
        {
            var (session, acls) = await _ubusClient.LoginWithAclsAsync(host, username, password, cancellationToken);
            return await _backupRestoreService.RestoreBackupAsync(host, session, backupFilePath, expectedChecksum, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ظپط´ظ„ ط§ظ„ط§طھطµط§ظ„ ظ„ط§ط³طھط¹ط§ط¯ط© ط§ظ„ظ†ط³ط®ط© ط§ظ„ط§ط­طھظٹط§ط·ظٹط© ظ„ظ„ط¬ظ‡ط§ط² {Host}", host);
            return Result<bool>.Failure($"ظپط´ظ„ ط§ظ„ط§طھطµط§ظ„: {ex.Message}", ErrorType.Unexpected, ex);
        }
    }
}
