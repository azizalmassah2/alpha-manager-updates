using System;
using System.Threading;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface IProgrammingService
    {
        /// <summary>
        /// برمجة جهاز OpenWrt واحد.
        /// </summary>
        /// <param name="canCommit">هل يُسمح بـ uci.commit عبر ACL؟</param>
        /// <param name="canApply">هل يُسمح بـ uci.apply عبر ACL؟</param>
        Task ProgramDeviceSingleAsync(
            string connectIp,
            string username,
            string password,
            string targetIp,
            string gateway,
            string subnetMask,
            int vlanId,
            WirelessConfig wirelessConfig,
            IProgress<(int percent, string message)> progress,
            CancellationToken cancellationToken,
            bool canCommit = true,
            bool canApply = true,
            bool changePassword = false,
            string newPassword = "",
            bool tryNetworkPasswordFirst = false);
    }
}
