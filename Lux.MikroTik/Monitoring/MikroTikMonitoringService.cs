using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Interfaces;
using Lux.MikroTik.Models;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Domain.Entities;
using Lux.MikroTik.Connectivity;

namespace Lux.MikroTik.Monitoring;

public class MikroTikMonitoringService : IDeviceMonitoringService
{
    private readonly IMikroTikTelemetryProvider _telemetryProvider;
    private readonly IMikroTikSessionManager _sessionManager;

    public MikroTikMonitoringService(
        IMikroTikTelemetryProvider telemetryProvider,
        IMikroTikSessionManager sessionManager)
    {
        _telemetryProvider = telemetryProvider;
        _sessionManager = sessionManager;
    }

    public async Task<Result<DeviceTelemetry>> GetTelemetryAsync(string deviceId, string host, string username, string password, CancellationToken cancellationToken = default)
    {
        var options = new MikroTikConnectionOptions
        {
            Host = host,
            Username = username,
            Password = password,
            UseSsl = false, // Or configure from elsewhere
            TimeoutSeconds = 10
        };

        try
        {
            await _sessionManager.OpenSessionAsync(options, cancellationToken);
            
            // Create a dummy IDevice instance using NetworkDevice just to carry the ID and IP
            var device = new NetworkDevice
            {
                Id = Guid.TryParse(deviceId, out var g) ? g : Guid.NewGuid(),
                IpAddress = host,
                Vendor = DeviceVendor.MikroTik
            };

            var result = await _telemetryProvider.GetTelemetryAsync(device, "default-session", cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            return Result<DeviceTelemetry>.Failure($"Monitoring failed: {ex.Message}", ErrorType.ExternalService);
        }
        finally
        {
            try
            {
                await _sessionManager.CloseSessionAsync(cancellationToken);
            }
            catch { /* Ignore disconnect errors */ }
        }
    }
}
