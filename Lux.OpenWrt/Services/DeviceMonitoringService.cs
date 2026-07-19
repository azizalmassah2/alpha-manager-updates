using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class DeviceMonitoringService : IDeviceMonitoringService
{
    private readonly IUbusClient _ubusClient;
    private readonly IOpenWrtTelemetryProvider _telemetryProvider;
    private readonly ILogger<DeviceMonitoringService> _logger;

    public DeviceMonitoringService(
        IUbusClient ubusClient, 
        IOpenWrtTelemetryProvider telemetryProvider, 
        ILogger<DeviceMonitoringService> logger)
    {
        _ubusClient = ubusClient;
        _telemetryProvider = telemetryProvider;
        _logger = logger;
    }

    public async Task<Result<DeviceTelemetry>> GetTelemetryAsync(string deviceId, string host, string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var (session, acls) = await _ubusClient.LoginWithAclsAsync(host, username, password, cancellationToken);
            if (string.IsNullOrEmpty(session))
            {
                return Result<DeviceTelemetry>.Failure("Invalid Session", ErrorType.Unauthorized);
            }

            var result = await _telemetryProvider.GetTelemetryAsync(deviceId, host, session, cancellationToken);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("Telemetry fetch timed out for device {Id}", deviceId);
            return Result<DeviceTelemetry>.Failure("Timeout", ErrorType.ExternalService, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect for telemetry: {Host}", host);
            return Result<DeviceTelemetry>.Failure($"Device Offline or Unreachable: {ex.Message}", ErrorType.Unexpected, ex);
        }
    }
}
