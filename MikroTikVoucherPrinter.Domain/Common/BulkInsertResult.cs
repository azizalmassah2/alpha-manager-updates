namespace MikroTikVoucherPrinter.Domain.Common;

public class BulkInsertResult
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> FailedUsernames { get; set; } = new List<string>();
}
