namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// وضع توليد الكروت
/// </summary>
public enum GenerationMode
{
    /// <summary>كروت بالكمية (Bulk)</summary>
    Bulk = 0,
    
    /// <summary>كرت واحد (Single)</summary>
    Single = 1
}
