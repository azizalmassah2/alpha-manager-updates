using System;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Interfaces.Telemetry;
using MikroTikVoucherPrinter.Domain.Entities.Telemetry;

namespace MikroTikVoucherPrinter.Application.Services.Telemetry;

public class DeviceHealthEvaluator : IDeviceHealthEvaluator
{
    public DeviceHealthStatus Evaluate(DeviceTelemetrySnapshot snapshot)
    {
        if (snapshot.Timestamp == default || (DateTime.UtcNow - snapshot.Timestamp).TotalMinutes > 5)
        {
            return DeviceHealthStatus.Offline;
        }

        double memoryUsagePercent = 0;
        if (snapshot.MemoryTotal > 0)
        {
            memoryUsagePercent = (double)snapshot.MemoryUsed / snapshot.MemoryTotal * 100;
        }

        if (memoryUsagePercent > 95 || snapshot.CpuUsage > 90)
        {
            return DeviceHealthStatus.Critical;
        }

        if (memoryUsagePercent > 80 || snapshot.CpuUsage > 75)
        {
            return DeviceHealthStatus.Warning;
        }

        return DeviceHealthStatus.Healthy;
    }
}
