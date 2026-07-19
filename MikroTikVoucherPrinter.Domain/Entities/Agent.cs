using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Domain.Entities;

/// <summary>
/// ط§ظ„ظˆظƒظٹظ„ / ط§ظ„ظ…ظˆط²ط¹ â€” ظٹطھطھط¨ط¹ ظ…ط¨ظٹط¹ط§طھظ‡ ظ…ظ† ط§ظ„ظƒط±ظˆطھ ظˆط¹ظ…ظˆظ„ط§طھظ‡
/// </summary>
public class Agent : BaseEntity
{
    public string Name        { get; set; } = string.Empty;
    public string Phone       { get; set; } = string.Empty;
    public string Notes       { get; set; } = string.Empty;

    /// <summary>ظ†ط³ط¨ط© ط§ظ„ط¹ظ…ظˆظ„ط© ظƒظ†ط³ط¨ط© ظ…ط¦ظˆظٹط© (ظ…ط«ظ„: 10 = 10%)</summary>
    public decimal CommissionRate { get; set; } = 0;

    public decimal Balance    { get; set; } = 0;
    public bool   IsActive    { get; set; } = true;
    public Guid   RouterId    { get; set; }

    // Navigation: ط§ظ„ظƒط±ظˆطھ ط§ظ„ظ…ط±طھط¨ط·ط© ط¨ظ‡ط°ط§ ط§ظ„ظˆظظٹظ„
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
