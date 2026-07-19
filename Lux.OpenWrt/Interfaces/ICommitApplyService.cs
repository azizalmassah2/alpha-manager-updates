using System.Threading;
using System.Threading.Tasks;

namespace Lux.OpenWrt.Interfaces;

public interface ICommitApplyService
{
    Task CommitAndApplyAsync(string ip, string session, bool canCommit, bool canApply, CancellationToken cancellationToken = default);
}
