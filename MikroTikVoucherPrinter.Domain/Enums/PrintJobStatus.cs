namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// حالة مهمة الطباعة ككل
/// </summary>
public enum PrintJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
