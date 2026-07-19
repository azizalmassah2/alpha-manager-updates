using System;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.State;

public interface IDeviceStateManager
{
    Task UpdateTelemetryAsync(Guid deviceId, double? cpuUsage, double? memoryUsage, int activeUsers);
    Task UpdateFirmwareAsync(Guid deviceId, string version);
    Task UpdateConfigurationAsync(Guid deviceId, string status);
    Task UpdateOperationResultAsync(Guid deviceId, bool success);
    Task SetDeviceOnlineAsync(Guid deviceId);
    Task SetDeviceOfflineAsync(Guid deviceId);
    
    /// <summary>
    /// Forces a refresh of the device state from telemetry services.
    /// </summary>
    Task RefreshAllDevicesAsync();
}
