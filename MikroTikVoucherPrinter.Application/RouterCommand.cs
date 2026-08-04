using System.Collections.Generic;

namespace MikroTikVoucherPrinter.Application;

/// <summary>
/// يمثل أمراً كاملاً لراوتر MikroTik مع مساره ومعاملاته.
/// استخدم هذا الوبجكت بدلاً من تمرير مسار الأمر كـ String مجرد.
/// </summary>
public sealed record RouterCommand
{
    /// <summary>
    /// مسار الأمر على RouterOS — مثل /user-manager/user/add
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// معاملات الأمر (key → value).
    /// مثال: { ["username"] = "john", ["profile"] = "1hour" }
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// هل يتطلب هذا الأمر إعادة اتصال بالراوتر بعد تنفيذه؟
    /// </summary>
    public bool RequiresReconnect { get; init; } = false;

    /// <summary>
    /// هل يدعم الأمر التنفيذ ضمن دُفعة (Batch / Transaction-like)?
    /// </summary>
    public bool SupportsTransaction { get; init; } = false;
}
