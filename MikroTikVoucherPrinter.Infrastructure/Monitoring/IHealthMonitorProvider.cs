using System;
using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Infrastructure.Monitoring
{
    public class MonitorResult
    {
        public bool IsSuccess { get; set; }
        public double LatencyMs { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public interface IHealthMonitorProvider
    {
        string MonitorType { get; }
        Task<MonitorResult> CheckHealthAsync(string target, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
