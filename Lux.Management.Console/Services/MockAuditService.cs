using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.Management.Console.Core;

namespace Lux.Management.Console.Services;

public class MockAuditService : IAuditService
{
    public Task RecordActionAsync(string actionName, string details, string? targetDeviceId = null, CancellationToken cancellationToken = default)
    {
        // For now, just a mock. Later we will save it to SQLite or API.
        System.Diagnostics.Debug.WriteLine($"[AUDIT] Action: {actionName}, Details: {details}, Device: {targetDeviceId}");
        return Task.CompletedTask;
    }
}
