namespace MikroTikVoucherPrinter.Domain.Enums
{
    public enum VoucherDeletedSource
    {
        Unknown = 0,
        LocalConsole = 1,
        RouterOS = 2,
        SystemCleanup = 3,
        ManualPurge = 4,
        SnapshotSync = 5
    }
}
