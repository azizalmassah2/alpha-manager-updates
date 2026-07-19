using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.OpenWrt.Models;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class ProgrammingService : IProgrammingService
{
    private readonly IOpenWrtDeviceManager _deviceManager;
    private readonly IUbusClient _ubus;
    private readonly IHostnameConfigurationService _hostname;
    private readonly INetworkConfigurationService _network;
    private readonly IVlanConfigurationService _vlan;
    private readonly IWirelessConfigurationService _wireless;
    private readonly ICommitApplyService _commitApply;
    private readonly IProgrammingRollbackService _rollback;
    private readonly ILogger<ProgrammingService> _logger;

    public ProgrammingService(
        IOpenWrtDeviceManager deviceManager,
        IUbusClient ubus,
        IHostnameConfigurationService hostname,
        INetworkConfigurationService network,
        IVlanConfigurationService vlan,
        IWirelessConfigurationService wireless,
        ICommitApplyService commitApply,
        IProgrammingRollbackService rollback,
        ILogger<ProgrammingService> logger)
    {
        _deviceManager = deviceManager;
        _ubus = ubus;
        _hostname = hostname;
        _network = network;
        _vlan = vlan;
        _wireless = wireless;
        _commitApply = commitApply;
        _rollback = rollback;
        _logger = logger;
    }

    public async Task<Result<bool>> ProgramDeviceAsync(
        string connectIp, string username, string password, string targetIp, string gateway,
        string subnetMask, int vlanId, WirelessConfig wirelessConfig, IProgress<ProgrammingProgress> progress,
        CancellationToken cancellationToken = default, bool canCommit = true, bool canApply = true,
        bool changePassword = false, string newPassword = "", bool tryNetworkPasswordFirst = false, bool createPreBackup = false)
    {
        string session = string.Empty;
        var totalSteps = createPreBackup ? 9 : 8;
        
        try
        {
            progress.Report(new ProgrammingProgress { CurrentStep = 1, TotalSteps = totalSteps, Message = "ط¬ط§ط±ظٹ ط§ظ„ط§طھطµط§ظ„ ظˆطھط³ط¬ظٹظ„ ط§ظ„ط¯ط®ظˆظ„ ظˆط§ط³طھط®ط±ط§ط¬ ط§ظ„طµظ„ط§ط­ظٹط§طھ...", Percentage = 10 });
            
            DeviceAcls acls = DeviceAcls.FullPermissions();
            string workingPassword = password;
            bool loginSuccess = false;

            if (tryNetworkPasswordFirst && !string.IsNullOrEmpty(newPassword))
            {
                try
                {
                    if (canCommit || canApply)
                    {
                        session = await _ubus.LoginAsync(connectIp, username, newPassword, cancellationToken);
                        acls = new DeviceAcls { CanGet = true, CanSet = true, CanAdd = true, CanDelete = true, CanCommit = canCommit, CanApply = canApply };
                    }
                    else
                    {
                        (session, acls) = await _ubus.LoginWithAclsAsync(connectIp, username, newPassword, cancellationToken);
                    }
                    loginSuccess = true;
                    workingPassword = newPassword;
                }
                catch { }
            }

            if (!loginSuccess)
            {
                if (canCommit || canApply)
                {
                    session = await _ubus.LoginAsync(connectIp, username, password, cancellationToken);
                    acls = new DeviceAcls { CanGet = true, CanSet = true, CanAdd = true, CanDelete = true, CanCommit = canCommit, CanApply = canApply };
                }
                else
                {
                    (session, acls) = await _ubus.LoginWithAclsAsync(connectIp, username, password, cancellationToken);
                }
                workingPassword = password;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!acls.CanGet || !acls.CanSet)
                return Result<bool>.Failure("ط§ظ„ط¬ظ‡ط§ط² ظ„ط§ ظٹظ…ظ†ط­ ط§ظ„ط­ط¯ ط§ظ„ط£ط¯ظ†ظ‰ ظ…ظ† ط§ظ„طµظ„ط§ط­ظٹط§طھ ط§ظ„ظ…ط·ظ„ظˆط¨ط© ظ„ظ„ط¨ط±ظ…ط¬ط© (uci.get + uci.set).", Lux.Platform.Abstractions.Common.ErrorType.Unauthorized);

            int currentStep = 2;
            
            if (createPreBackup)
            {
                progress.Report(new ProgrammingProgress { CurrentStep = currentStep++, TotalSteps = totalSteps, Message = "ط¬ط§ط±ظٹ ط¥ظ†ط´ط§ط، ظ†ط³ط®ط© ط§ط­طھظٹط§ط·ظٹط© (PreProgramming Backup)...", Percentage = 15 });
                var backupResult = await _deviceManager.CreateBackupAsync(connectIp, username, workingPassword, BackupType.Configuration, cancellationToken);
                if (!backupResult.IsSuccess)
                {
                    _logger.LogWarning("ظپط´ظ„ ط¥ظ†ط´ط§ط، ط§ظ„ظ†ط³ط®ط© ط§ظ„ط§ط­طھظٹط§ط·ظٹط© ظ‚ط¨ظ„ ط§ظ„ط¨ط±ظ…ط¬ط©: {Message}. ط§ظ„ظ…طھط§ط¨ط¹ط© ظپظٹ ط§ظ„ط¨ط±ظ…ط¬ط©...", backupResult.ErrorMessage);
                }
            }

            progress.Report(new ProgrammingProgress { CurrentStep = currentStep++, TotalSteps = totalSteps, Message = "ط¬ط§ط±ظٹ ط§ظƒطھط´ط§ظپ ط§ظ„ط¨ظ†ظٹط© ط§ظ„ط¨ط±ظ…ط¬ظٹط© ظˆط§ظ„ط´ط¨ظƒظٹط© ظ„ظ„ط¬ظ‡ط§ط²...", Percentage = 20 });
            var discoveryResult = await _deviceManager.DiscoverDeviceAsync(connectIp, username, workingPassword, cancellationToken);
            if (!discoveryResult.IsSuccess)
                return Result<bool>.Failure(discoveryResult.ErrorMessage ?? "ظپط´ظ„ ط§ظƒطھط´ط§ظپ ط§ظ„ط¬ظ‡ط§ط²", discoveryResult.ErrorType);

            cancellationToken.ThrowIfCancellationRequested();

            var infoDoc = JsonDocument.Parse(discoveryResult.Value!.Metadata!);
            var info = infoDoc.RootElement;
            var lanSection = info.GetProperty("LanSectionName").GetString() ?? "";
            var lanDevice = info.GetProperty("LanDeviceName").GetString() ?? "";
            var vlanTypeStr = info.GetProperty("VlanType").GetString() ?? "Traditional";
            var switchName = info.GetProperty("SwitchName").GetString() ?? "switch0";
            var switchCpu = info.GetProperty("SwitchCpuPort").GetString() ?? "";
            var switchLan = info.GetProperty("SwitchLanPorts").GetString() ?? "";
            var r24Name = info.GetProperty("Radio24GhzName").GetString() ?? "";
            var r5Name = info.GetProperty("Radio5GhzName").GetString() ?? "";
            var wifi24Sec = info.GetProperty("WifiIface24GhzSection").GetString() ?? "";
            var wifi5Sec = info.GetProperty("WifiIface5GhzSection").GetString() ?? "";

            progress.Report(new ProgrammingProgress { CurrentStep = currentStep++, TotalSteps = totalSteps, Message = "ط¬ط§ط±ظٹ ط¶ط¨ط· ط§ط³ظ… ط§ظ„ظ…ط¶ظٹظپ (Hostname)...", Percentage = 30 });
            await _hostname.ConfigureHostnameAsync(connectIp, session, targetIp, cancellationToken);

            progress.Report(new ProgrammingProgress { CurrentStep = currentStep++, TotalSteps = totalSteps, Message = "ط¬ط§ط±ظٹ ط¥ط¹ط¯ط§ط¯ ط§ظ„ط¹ظ†ط§ظˆظٹظ† ظˆط§ظ„ط´ط¨ظƒط© ط§ظ„ظ…ط­ظ„ظٹط© ظˆ VLAN...", Percentage = 45 });
            await _network.SetLanIpAsync(connectIp, session, lanSection, targetIp, gateway, subnetMask, cancellationToken);
            await _vlan.CreateVlanAsync(connectIp, session, lanDevice, vlanTypeStr, vlanId, switchName, switchCpu, switchLan, cancellationToken);
            await _network.DisableDhcpAsync(connectIp, session, lanSection, cancellationToken);

            progress.Report(new ProgrammingProgress { CurrentStep = currentStep++, TotalSteps = totalSteps, Message = "ط¬ط§ط±ظٹ ط¶ط¨ط· ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ ط§ظ„ظ„ط§ط³ظ„ظƒظٹط© (Wi-Fi)...", Percentage = 65 });
            var apPassword = wirelessConfig.IsEncrypted ? wirelessConfig.WifiPassword : string.Empty;
            await _wireless.ConfigureRadioApAsync(connectIp, session, r24Name, wifi24Sec, wirelessConfig.Ssid24Ghz, apPassword, $"vlan{vlanId}", cancellationToken);
            
            if (wirelessConfig.Mode == WirelessMode.AccessPoint)
            {
                await _wireless.ConfigureRadioApAsync(connectIp, session, r5Name, wifi5Sec, wirelessConfig.Ssid5Ghz, apPassword, "lan", cancellationToken);
            }
            else
            {
                await _wireless.ConfigureRadioStaWdsAsync(connectIp, session, r5Name, wifi5Sec, wirelessConfig.RemoteSsid, wirelessConfig.RemotePassword, "lan", cancellationToken);
            }

            progress.Report(new ProgrammingProgress { CurrentStep = currentStep++, TotalSteps = totalSteps, Message = "ط¬ط§ط±ظٹ ط­ظپط¸ ظˆطھط·ط¨ظٹظ‚ ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ ط§ظ„ط¬ط¯ظٹط¯ط©...", Percentage = 80 });
            await _commitApply.CommitAndApplyAsync(connectIp, session, acls.CanCommit, acls.CanApply, cancellationToken);

            progress.Report(new ProgrammingProgress { CurrentStep = currentStep++, TotalSteps = totalSteps, Message = "ط¬ط§ط±ظٹ ط§ظ„طھط±طھظٹط¨ ط§ظ„ظ†ظ‡ط§ط¦ظٹ ظˆطھط؛ظٹظٹط± ظƒظ„ظ…ط© ط§ظ„ظ…ط±ظˆط± ط¥ظ† ط·ظ„ط¨...", Percentage = 95 });
            if (changePassword && !string.IsNullOrEmpty(newPassword))
            {
                try { await _ubus.CallAsync(connectIp, session, "luci", "setPassword", new { username = "root", password = newPassword }, cancellationToken); }
                catch { }
            }

            progress.Report(new ProgrammingProgress { CurrentStep = currentStep++, TotalSteps = totalSteps, Message = "ط§ظƒطھظ…ظ„طھ ط§ظ„ط¨ط±ظ…ط¬ط© ط¨ظ†ط¬ط§ط­!", Percentage = 100 });
            return Result<bool>.Success(true);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("طھظ… ط¥ظ„ط؛ط§ط، ط¹ظ…ظ„ظٹط© ط§ظ„ط¨ط±ظ…ط¬ط©.");
            await _rollback.RollbackAsync(connectIp, session, CancellationToken.None);
            return Result<bool>.Failure("طھظ… ط§ظ„ط¥ظ„ط؛ط§ط، ط£ظˆ ط§ظ†طھظ‡ط§ط، ط§ظ„ظ…ظ‡ظ„ط©.", Lux.Platform.Abstractions.Common.ErrorType.ExternalService, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ط®ط·ط£ ط؛ظٹط± ظ…طھظˆظ‚ط¹ ط£ط«ظ†ط§ط، ط§ظ„ط¨ط±ظ…ط¬ط©. ط³ظٹطھظ… ظ…ط­ط§ظˆظ„ط© ط¥ط¬ط±ط§ط، Rollback...");
            await _rollback.RollbackAsync(connectIp, session, CancellationToken.None);
            return Result<bool>.Failure($"ط®ط·ط£ ط£ط«ظ†ط§ط، ط¨ط±ظ…ط¬ط© ط§ظ„ط¬ظ‡ط§ط²: {ex.Message}", Lux.Platform.Abstractions.Common.ErrorType.Unexpected, ex);
        }
    }
}
