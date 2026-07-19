using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Interfaces;
using Lux.MikroTik.Interfaces;
using Lux.MikroTik.Connectivity;
using Lux.MikroTik.Models;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.MikroTik.Services;

public class MikroTikDeviceManager : IMikroTikDeviceManager
{
    private readonly IDeviceMonitoringService _monitoringService;
    private readonly IDeviceTelemetryProvider _telemetryProvider;
    private readonly IMikroTikSessionManager _sessionManager;
    private readonly IMikroTikDiscoveryService _discoveryService;

    public MikroTikDeviceManager(
        IDeviceMonitoringService monitoringService,
        IDeviceTelemetryProvider telemetryProvider,
        IMikroTikSessionManager sessionManager,
        IMikroTikDiscoveryService discoveryService)
    {
        _monitoringService = monitoringService;
        _telemetryProvider = telemetryProvider;
        _sessionManager = sessionManager;
        _discoveryService = discoveryService;
    }

    public DeviceVendor SupportedVendor => DeviceVendor.MikroTik;

    public async Task<DeviceStatus> CheckStatusAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(ipAddress, 2000).WaitAsync(cancellationToken);
            return reply.Status == System.Net.NetworkInformation.IPStatus.Success ? DeviceStatus.Online : DeviceStatus.Offline;
        }
        catch
        {
            return DeviceStatus.Offline;
        }
    }

    public async Task<IDevice> DiscoverDeviceAsync(string ipAddress, string username, string password, CancellationToken cancellationToken = default)
    {
        var options = new MikroTikConnectionOptions
        {
            Host = ipAddress,
            Username = username,
            Password = password,
            UseSsl = false,
            TimeoutSeconds = 5
        };

        await _sessionManager.OpenSessionAsync(options, cancellationToken);
        try
        {
            var dummyDevice = new NetworkDevice
            {
                IpAddress = ipAddress,
                Vendor = DeviceVendor.MikroTik
            };

            var result = await _discoveryService.DiscoverAsync(dummyDevice, cancellationToken);
            if (result.IsFailure)
            {
                throw new Exception($"Discovery failed: {result.ErrorMessage}");
            }

            return result.Value;
        }
        finally
        {
            await _sessionManager.CloseSessionAsync(cancellationToken);
        }
    }
}
