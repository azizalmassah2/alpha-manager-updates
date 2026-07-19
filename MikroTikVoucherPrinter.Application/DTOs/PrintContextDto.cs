namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// سياق الطباعة — يحتوي على بيانات ثابتة لا تتغير بين الكروت في نفس عملية الطباعة.
/// يُستخدم لحل FieldTokens المرتبطة بالشبكة والوكيل والتاريخ.
/// </summary>
public sealed class PrintContextDto
{
    // ══ بيانات الشبكة ══

    /// <summary>اسم الشبكة / SSID</summary>
    public string NetworkName { get; init; } = string.Empty;

    /// <summary>اسم الراوتر</summary>
    public string RouterName { get; init; } = string.Empty;

    /// <summary>عنوان IP الراوتر</summary>
    public string RouterIp { get; init; } = string.Empty;

    // ══ بيانات الوكيل (اختياري) ══

    /// <summary>اسم الوكيل المسؤول عن هذه الطباعة</summary>
    public string? AgentName { get; init; }

    /// <summary>رقم هاتف الوكيل</summary>
    public string? AgentPhone { get; init; }

    // ══ توقيت الطباعة ══

    /// <summary>تاريخ ووقت بدء عملية الطباعة (يُحدَّد مرة واحدة للدفعة كلها)</summary>
    public DateTime PrintedAt { get; init; } = DateTime.Now;

    // ══ بيانات الدفعة ══

    /// <summary>رقم الدفعة (اختياري)</summary>
    public string? BatchNumber { get; init; }

    /// <summary>إجمالي عدد الكروت في هذه الطباعة</summary>
    public int TotalCards { get; init; }

    // ══ ملفات ══

    /// <summary>مسار ملف شعار الشركة/الراوتر (اختياري)</summary>
    public string? CompanyLogoPath { get; init; }

    // ══ Computed ══

    public string PrintDateDisplay => PrintedAt.ToString("dd/MM/yyyy");
    public string PrintTimeDisplay => PrintedAt.ToString("HH:mm");
}
