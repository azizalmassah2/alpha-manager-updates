using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Infrastructure.Monitoring
{
    public class PingHealthMonitorProvider : IHealthMonitorProvider
    {
        public string MonitorType => "Ping";

        public async Task<MonitorResult> CheckHealthAsync(string target, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(target, (int)timeout.TotalMilliseconds).WaitAsync(cancellationToken);
                
                if (reply.Status == IPStatus.Success)
                {
                    return new MonitorResult
                    {
                        IsSuccess = true,
                        LatencyMs = reply.RoundtripTime
                    };
                }

                return new MonitorResult
                {
                    IsSuccess = false,
                    ErrorMessage = reply.Status.ToString()
                };
            }
            catch (Exception ex)
            {
                return new MonitorResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
