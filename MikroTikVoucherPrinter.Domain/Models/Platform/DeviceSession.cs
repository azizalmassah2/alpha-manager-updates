using System;
using MikroTikVoucherPrinter.Domain.Enums.Platform;

namespace MikroTikVoucherPrinter.Domain.Models.Platform;

public class DeviceSession : IAsyncDisposable
{
    public Guid DeviceId { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool IsConnected { get; set; }
    public DeviceHealthStatus HealthStatus { get; set; }

    /// <summary>
    /// The underlying protocol connection instance (e.g., MikroTik API connection)
    /// </summary>
    public IAsyncDisposable? ConnectionInstance { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (ConnectionInstance != null)
        {
            await ConnectionInstance.DisposeAsync();
        }
        IsConnected = false;
    }
}
