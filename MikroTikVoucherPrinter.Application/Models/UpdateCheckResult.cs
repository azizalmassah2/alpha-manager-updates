namespace MikroTikVoucherPrinter.Application.Models;

/// <summary>
/// نتيجة فحص التحديثات — تُغلّف معلومات التحديث مع القرار الواجب اتخاذه.
///
/// السيناريوهات الممكنة:
///   NoUpdate       — البرنامج محدَّث أو فشل الاتصال
///   UpdateAvailable — يوجد تحديث (اختياري / موصى به / أمني)
///   UpdateRequired  — التحديث إجباري (mandatory أو إصدار أقل من minimumSupportedVersion)
/// </summary>
public sealed class UpdateCheckResult
{
    // ── Singleton ─────────────────────────────────────────────────────────
    /// <summary>لا يوجد تحديث أو لم يكن بالإمكان فحصه</summary>
    public static readonly UpdateCheckResult NoUpdate = new(null, false);

    // ── Properties ────────────────────────────────────────────────────────
    /// <summary>بيانات التحديث المتوفر (null إذا لا يوجد تحديث)</summary>
    public UpdateInfo? Update { get; }

    /// <summary>
    /// True إذا كان إصدار العميل الحالي أقل من MinimumSupportedVersion.
    /// في هذه الحالة يُعامَل التحديث كإجباري حتى لو Mandatory=false.
    /// </summary>
    public bool IsVersionBlocked { get; }

    // ── Computed ──────────────────────────────────────────────────────────
    /// <summary>هل يوجد تحديث فعلاً؟</summary>
    public bool HasUpdate => Update != null;

    /// <summary>
    /// هل يجب منع المستخدم من تجاوز التحديث؟
    /// يجمع قواعد: Mandatory + Security + minimumSupportedVersion
    /// </summary>
    public bool MustUpdate => HasUpdate && (Update!.CannotSkip || IsVersionBlocked);

    // ── Constructor ───────────────────────────────────────────────────────
    private UpdateCheckResult(UpdateInfo? update, bool isVersionBlocked)
    {
        Update           = update;
        IsVersionBlocked = isVersionBlocked;
    }

    // ── Factory ───────────────────────────────────────────────────────────
    /// <summary>يوجد تحديث متاح</summary>
    public static UpdateCheckResult Available(UpdateInfo update, bool isVersionBlocked = false)
        => new(update, isVersionBlocked);
}
