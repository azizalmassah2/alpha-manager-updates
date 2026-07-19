using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lux.Management.Console.Core;

public interface IAuditService
{
    Task RecordActionAsync(string actionName, string details, string? targetDeviceId = null, CancellationToken cancellationToken = default);
}
