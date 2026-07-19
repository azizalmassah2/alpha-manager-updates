namespace MikroTikVoucherPrinter.Domain.Interfaces;

/// <summary>
/// واجهة خدمة الحوارات والرسائل
/// </summary>
public interface IDialogService
{
    /// <summary>عرض رسالة معلوماتية</summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>عرض رسالة خطأ</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>عرض رسالة تأكيد</summary>
    Task<bool> ShowConfirmAsync(string title, string message);

    /// <summary>عرض رسالة تأكيد مع ثلاث خيارات</summary>
    Task<bool?> ShowConfirmWithCancelAsync(string title, string message);
}
