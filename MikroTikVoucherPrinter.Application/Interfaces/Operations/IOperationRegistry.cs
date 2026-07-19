using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.Interfaces.Operations;

public interface IOperationRegistry
{
    IDeviceOperation ResolveOperation(OperationType type);
    bool IsOperationSupported(OperationType type);
}
