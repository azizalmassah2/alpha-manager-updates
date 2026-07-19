namespace MikroTikVoucherPrinter.Application.Models;

/// <summary>
/// نوع التحديث — يحدد سلوك نافذة التحديث وأزرارها
/// </summary>
public enum UpdateType
{
    /// <summary>اختياري — يمكن التخطي في أي وقت</summary>
    Optional,

    /// <summary>موصى به — يُشجَّع على التحديث مع إمكانية التخطي</summary>
    Recommended,

    /// <summary>إجباري — يمنع متابعة البرنامج حتى يتم التحديث</summary>
    Mandatory,

    /// <summary>أمني — يعرض تحذيراً أمنياً خاصاً مع توصية قوية</summary>
    Security
}
