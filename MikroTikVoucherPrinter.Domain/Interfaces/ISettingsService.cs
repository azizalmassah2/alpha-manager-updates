namespace MikroTikVoucherPrinter.Domain.Interfaces;

/// <summary>
/// واجهة خدمة الإعدادات
/// </summary>
public interface ISettingsService
{
    /// <summary>جلب قيمة إعداد</summary>
    T Get<T>(string key, T defaultValue = default!);

    /// <summary>حفظ قيمة إعداد</summary>
    void Set<T>(string key, T value);

    /// <summary>حفظ جميع التغييرات</summary>
    Task SaveAsync();

    /// <summary>تحميل الإعدادات</summary>
    Task LoadAsync();
}
