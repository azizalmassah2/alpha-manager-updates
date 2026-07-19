using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities.Platform;

namespace Lux.Management.Console.Modules.MikroTik.Connections.Services;

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string? Reason { get; set; }
    public long LatencyMs { get; set; }
    public string? RouterIdentity { get; set; }
    public string? RouterOSVersion { get; set; }
}

public interface IConnectionTestService
{
    Task<ConnectionTestResult> TestConnectionAsync(Router router, CancellationToken cancellationToken = default);
}
