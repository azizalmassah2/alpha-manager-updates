using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Domain.Entities;

public class Batch : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public Guid RouterId { get; set; }

    // Navigation
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
