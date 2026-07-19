using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions.Interfaces;
using Lux.OpenWrt.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class OpenWrtTelemetryProvider : IOpenWrtTelemetryProvider, IDeviceTelemetryProvider
{
    private readonly IUbusClient _ubusClient;
    private readonly ILogger<OpenWrtTelemetryProvider> _logger;

    public OpenWrtTelemetryProvider(IUbusClient ubusClient, ILogger<OpenWrtTelemetryProvider> logger)
    {
        _ubusClient = ubusClient;
        _logger = logger;
    }

    public Task<Result<DeviceTelemetry>> GetTelemetryAsync(IDevice device, string session, CancellationToken cancellationToken = default)
    {
        return GetTelemetryAsync(device.Id.ToString(), device.IpAddress, session, cancellationToken);
    }

    public async Task<Result<DeviceTelemetry>> GetTelemetryAsync(string deviceId, string ip, string session, CancellationToken cancellationToken = default)
    {
        try
        {
            var telemetry = new DeviceTelemetry
            {
                DeviceId = deviceId,
                Timestamp = DateTime.UtcNow,
                Status = "Online"
            };

            // 1. Get System Info (CPU, Memory, Uptime)
            try
            {
                var sysInfo = await _ubusClient.CallAsync(ip, session, "system", "info", null, cancellationToken);
                if (sysInfo.ValueKind != JsonValueKind.Null)
                {
                    if (sysInfo.TryGetProperty("uptime", out var uptimeProp))
                        telemetry.Uptime = TimeSpan.FromSeconds(uptimeProp.GetInt32());

                    if (sysInfo.TryGetProperty("memory", out var memProp))
                    {
                        var total = memProp.TryGetProperty("total", out var t) ? t.GetInt64() : 0;
                        var free = memProp.TryGetProperty("free", out var f) ? f.GetInt64() : 0;
                        var cached = memProp.TryGetProperty("cached", out var c) ? c.GetInt64() : 0;
                        var buffered = memProp.TryGetProperty("buffered", out var b) ? b.GetInt64() : 0;

                        telemetry.MemoryTotalMb = total / (1024.0 * 1024.0);
                        var used = total - free - cached - buffered;
                        if (used < 0) used = total - free; // fallback

                        telemetry.MemoryUsedMb = used / (1024.0 * 1024.0);
                        if (total > 0)
                            telemetry.MemoryUsagePercent = Math.Round(((double)used / total) * 100, 2);
                    }

                    if (sysInfo.TryGetProperty("load", out var loadProp) && loadProp.ValueKind == JsonValueKind.Array && loadProp.GetArrayLength() > 0)
                    {
                        // load array is typically [ 1min, 5min, 15min ] multiplied by 65535
                        var load1 = loadProp[0].GetInt32();
                        telemetry.CpuUsagePercent = Math.Round((load1 / 65535.0) * 100, 2); 
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get system info for telemetry on {Ip}", ip);
                telemetry.Status = "PartialData";
            }

            // 2. Get Board Info (Firmware Version)
            try
            {
                var boardInfo = await _ubusClient.CallAsync(ip, session, "system", "board", null, cancellationToken);
                if (boardInfo.ValueKind != JsonValueKind.Null && boardInfo.TryGetProperty("release", out var releaseProp))
                {
                    if (releaseProp.TryGetProperty("description", out var descProp))
                        telemetry.FirmwareVersion = descProp.GetString() ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get board info for telemetry on {Ip}", ip);
            }

            // 3. Get Wireless Info (Clients, Signal, Rates)
            try
            {
                var devicesInfo = await _ubusClient.CallAsync(ip, session, "iwinfo", "devices", null, cancellationToken);
                if (devicesInfo.ValueKind == JsonValueKind.Array)
                {
                    int totalClients = 0;
                    double sumSignal = 0;
                    double sumNoise = 0;
                    double sumTx = 0;
                    double sumRx = 0;
                    int validSignalCount = 0;

                    foreach (var devProp in devicesInfo.EnumerateArray())
                    {
                        var dev = devProp.GetString();
                        if (string.IsNullOrEmpty(dev)) continue;

                        var assocInfo = await _ubusClient.CallAsync(ip, session, "iwinfo", "assoclist", new { device = dev }, cancellationToken);
                        if (assocInfo.ValueKind != JsonValueKind.Null && assocInfo.TryGetProperty("results", out var resultsProp) && resultsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var client in resultsProp.EnumerateArray())
                            {
                                totalClients++;
                                if (client.TryGetProperty("signal", out var sig)) { sumSignal += sig.GetInt32(); validSignalCount++; }
                                if (client.TryGetProperty("noise", out var noise)) { sumNoise += noise.GetInt32(); }
                                
                                if (client.TryGetProperty("tx_rate", out var tx) && tx.TryGetProperty("rate", out var txr)) sumTx += txr.GetInt32();
                                if (client.TryGetProperty("rx_rate", out var rx) && rx.TryGetProperty("rate", out var rxr)) sumRx += rxr.GetInt32();
                            }
                        }
                    }

                    telemetry.ConnectedClients = totalClients;
                    if (validSignalCount > 0)
                    {
                        telemetry.SignalStrength = Math.Round(sumSignal / validSignalCount, 1);
                        telemetry.NoiseFloor = Math.Round(sumNoise / validSignalCount, 1);
                        telemetry.TxRate = Math.Round(sumTx / validSignalCount / 1000.0, 1); // Mbps
                        telemetry.RxRate = Math.Round(sumRx / validSignalCount / 1000.0, 1); // Mbps
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get wireless info for telemetry on {Ip}", ip);
            }

            return Result<DeviceTelemetry>.Success(telemetry);
        }
        catch (OperationCanceledException)
        {
            return Result<DeviceTelemetry>.Failure("Timeout while fetching telemetry", Lux.Platform.Abstractions.Common.ErrorType.ExternalService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to completely fetch telemetry from {Ip}", ip);
            return Result<DeviceTelemetry>.Failure(ex.Message, Lux.Platform.Abstractions.Common.ErrorType.Unexpected);
        }
    }
}
