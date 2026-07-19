namespace MikroTikVoucherPrinter.Domain.Enums;

public enum OperationType
{
    Unknown = 0,
    Backup = 1,
    Reboot = 2,
    HealthCheck = 3,
    ScriptExecution = 4,
    FirmwareUpgrade = 5,
    SignalCheck = 6,
    FactoryReset = 7
}
