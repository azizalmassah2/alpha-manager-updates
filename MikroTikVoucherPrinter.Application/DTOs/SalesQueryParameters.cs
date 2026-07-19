using System;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// معاملات الاستعلام لشاشة المبيعات
/// </summary>
public record SalesQueryParameters
{
    /// <summary>مسار قاعدة بيانات User Manager للراوتر الحالي</summary>
    public string RouterDbPath { get; set; } = string.Empty;

    /// <summary>فلتر التاريخ: null = كل التواريخ، تاريخ محدد = عرض يوم واحد</summary>
    public DateOnly? FilterDate { get; set; }

    /// <summary>نص البحث (رقم الكرت، اسم الباقة)</summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>فلتر الحالة: "" = الكل، "active" = نشط، "expired" = منتهي، "paused" = موقوف</summary>
    public string FilterStatus { get; set; } = string.Empty;

    /// <summary>فلتر الباقة المختارة: "كل الباقات" أو اسم الباقة الفعلي</summary>
    public string FilterProfile { get; set; } = string.Empty;

    /// <summary>Keyset Pagination: آخر activated محمَّل (Unix timestamp)</summary>
    public long? AfterActivated { get; set; }

    /// <summary>Keyset Pagination: آخر id محمَّل (tie-breaker)</summary>
    public int? AfterId { get; set; }

    /// <summary>حجم الصفحة</summary>
    public int PageSize { get; set; } = 50;
}
