using System;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Services.Telemetry;
using MikroTikVoucherPrinter.Domain.Entities.Telemetry;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests.State;

public class DeviceHealthEvaluatorTests
{
    private readonly DeviceHealthEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ShouldReturnOffline_WhenTimestampDefault()
    {
        var state = new DeviceTelemetrySnapshot { Timestamp = default };
        var health = _evaluator.Evaluate(state);
        Assert.Equal(DeviceHealthStatus.Offline, health);
    }

    [Fact]
    public void Evaluate_ShouldReturnOffline_WhenLastSeenExceeds5Minutes()
    {
        var state = new DeviceTelemetrySnapshot { Timestamp = DateTime.UtcNow.AddMinutes(-6) };
        var health = _evaluator.Evaluate(state);
        Assert.Equal(DeviceHealthStatus.Offline, health);
    }

    [Fact]
    public void Evaluate_ShouldReturnCritical_WhenCpuExceeds90()
    {
        var state = new DeviceTelemetrySnapshot { Timestamp = DateTime.UtcNow, CpuUsage = 95 };
        var health = _evaluator.Evaluate(state);
        Assert.Equal(DeviceHealthStatus.Critical, health);
    }

    [Fact]
    public void Evaluate_ShouldReturnWarning_WhenMemoryExceeds80()
    {
        var state = new DeviceTelemetrySnapshot { Timestamp = DateTime.UtcNow, MemoryUsed = 85, MemoryTotal = 100, CpuUsage = 50 };
        var health = _evaluator.Evaluate(state);
        Assert.Equal(DeviceHealthStatus.Warning, health);
    }

    [Fact]
    public void Evaluate_ShouldReturnHealthy_WhenMetricsAreNormal()
    {
        var state = new DeviceTelemetrySnapshot { Timestamp = DateTime.UtcNow, MemoryUsed = 50, MemoryTotal = 100, CpuUsage = 50 };
        var health = _evaluator.Evaluate(state);
        Assert.Equal(DeviceHealthStatus.Healthy, health);
    }
}
