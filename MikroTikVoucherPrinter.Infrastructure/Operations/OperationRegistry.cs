using System;
using Microsoft.Extensions.DependencyInjection;
using MikroTikVoucherPrinter.Application.Interfaces.Operations;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Infrastructure.Operations;

public class OperationRegistry : IOperationRegistry
{
    private readonly IServiceProvider _serviceProvider;

    public OperationRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IDeviceOperation ResolveOperation(OperationType type)
    {
        return type switch
        {
            OperationType.Backup => _serviceProvider.GetRequiredService<MikroTikVoucherPrinter.Application.Operations.Router.RouterBackupOperation>(),
            OperationType.Reboot => _serviceProvider.GetRequiredService<MikroTikVoucherPrinter.Application.Operations.Modem.BatchModemRebootOperation>(),
            _ => throw new NotSupportedException($"Operation type {type} is not registered in the registry.")
        };
    }

    public bool IsOperationSupported(OperationType type)
    {
        return type == OperationType.Backup || type == OperationType.Reboot;
    }
}
