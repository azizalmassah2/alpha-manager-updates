using System;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.State;

namespace MikroTikVoucherPrinter.Infrastructure.State;

public class DeviceHealthEvaluator : IDeviceHealthEvaluator
{
    public DeviceHealthStatus Evaluate(DeviceState state)
    {
        if (!state.IsOnline || (DateTime.UtcNow - state.LastSeen).TotalMinutes > 5)
        {
            return DeviceHealthStatus.Offline;
        }

        if (state.MemoryUsage > 95 || state.CpuUsage > 90)
        {
            return DeviceHealthStatus.Critical;
        }

        if (state.MemoryUsage > 80 || state.CpuUsage > 75)
        {
            return DeviceHealthStatus.Warning;
        }

        return DeviceHealthStatus.Healthy;
    }
}
