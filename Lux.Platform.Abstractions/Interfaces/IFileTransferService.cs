using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;

namespace Lux.Platform.Abstractions.Interfaces;

public interface IFileTransferService
{
    bool CanHandle(IDevice device);
    Task<Result> UploadAsync(IDevice device, string localPath, string remotePath, CancellationToken cancellationToken = default);
}
