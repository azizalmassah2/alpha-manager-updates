using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.State;

public interface IDeviceHealthEvaluator
{
    DeviceHealthStatus Evaluate(DeviceState state);
}
