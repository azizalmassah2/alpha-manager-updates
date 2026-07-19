namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// حالة التزامن مع سيرفر المايكروتك
/// </summary>
public enum SyncStatus
{
    /// <summary>في انتظار المزامنة (الكرت موجودمحلياً فقط)</summary>
    Pending = 0,
    
    /// <summary>تمت المزامنة بنجاح وحفظ الـ MikroTikUserId</summary>
    Synced = 1,
    
    /// <summary>فشلت المزامنة مؤخراً ويجب إعادة المحاولة</summary>
    Failed = 2
}
