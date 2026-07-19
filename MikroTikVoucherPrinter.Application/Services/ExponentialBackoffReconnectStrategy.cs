using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Interfaces;

namespace MikroTikVoucherPrinter.Application.Services;

public class ExponentialBackoffReconnectStrategy : IReconnectStrategy
{
    private readonly TimeSpan[] _backoffIntervals = new[]
    {
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30)
    };

    public async Task<bool> WaitForReconnectAsync(IDevice device, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var timeoutLimit = DateTime.UtcNow.Add(timeout);
        int attempt = 0;

        while (DateTime.UtcNow < timeoutLimit)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            // wait according to backoff strategy
            var delay = attempt < _backoffIntervals.Length ? _backoffIntervals[attempt] : _backoffIntervals[^1];
            await Task.Delay(delay, cancellationToken);

            attempt++;

            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(device.IpAddress, 2000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    return true;
                }
            }
            catch
            {
                // ignore
            }
        }

        return false;
    }
}
