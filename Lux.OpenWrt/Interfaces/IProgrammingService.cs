using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Models;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;

namespace Lux.OpenWrt.Interfaces;

public interface IProgrammingService
{
    Task<Result<bool>> ProgramDeviceAsync(
        string connectIp,
        string username,
        string password,
        string targetIp,
        string gateway,
        string subnetMask,
        int vlanId,
        WirelessConfig wirelessConfig,
        IProgress<ProgrammingProgress> progress,
        CancellationToken cancellationToken = default,
        bool canCommit = true,
        bool canApply = true,
        bool changePassword = false,
        string newPassword = "",
        bool tryNetworkPasswordFirst = false,
        bool createPreBackup = false);
}
