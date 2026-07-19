using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Domain.Entities.Telemetry;

namespace MikroTikVoucherPrinter.Application.Interfaces.Telemetry;

public interface IDeviceHealthEvaluator
{
    DeviceHealthStatus Evaluate(DeviceTelemetrySnapshot snapshot);
}
