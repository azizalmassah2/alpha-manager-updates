using System;

namespace MikroTikVoucherPrinter.Domain.Interfaces;

/// <summary>
/// يمثل حالة التنقل الحالية، مفصول عن خدمة التنقل لغرض الـ Testing والـ Data Binding
/// </summary>
public interface INavigationState
{
    /// <summary>نوع الـ ViewModel الحالي</summary>
    Type? CurrentViewModel { get; set; }

    /// <summary>حدث تغيير الحالة</summary>
    event Action? StateChanged;
}
