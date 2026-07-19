namespace MikroTikVoucherPrinter.Domain.Interfaces;

/// <summary>
/// واجهة خدمة التنقل بين الصفحات
/// </summary>
public interface INavigationService
{
    /// <summary>الصفحة الحالية</summary>
    string CurrentPage { get; }

    /// <summary>التنقل لصفحة معينة</summary>
    void NavigateTo(string pageKey);

    /// <summary>التنقل مع تمرير بيانات</summary>
    void NavigateTo(string pageKey, object parameter);

    /// <summary>الرجوع للصفحة السابقة</summary>
    bool CanGoBack { get; }
    void GoBack();

    /// <summary>حدث تغيير الصفحة</summary>
    event Action<string>? PageChanged;
}
