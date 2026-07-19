namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// الخطوة التشغيلية الحالية لمهمة الطباعة
/// </summary>
public enum PrintJobStep
{
    GeneratingVouchers = 0,
    SyncingRouter = 1,
    BuildingPdf = 2,
    Printing = 3,
    Completed = 4
}
