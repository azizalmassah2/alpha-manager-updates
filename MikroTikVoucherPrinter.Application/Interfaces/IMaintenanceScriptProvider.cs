namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// مزود اسكريبتات الصيانة الجاهزة للرفع على /system/scheduler في المايكروتيك.
/// مفصول تماماً عن IMikroTikCommandProvider لأن هذه Scripts كاملة وليست Commands أفردية.
/// </summary>
public interface IMaintenanceScriptProvider
{
    // ── Script Names (يُستخدم في /system/script وScheduler) ──────────────

    /// <summary>اسم الاسكريبت على الراوتر لحذف كروت الرصيد المستنفدة</summary>
    string CleanQuotaScriptName { get; }

    /// <summary>اسم الاسكريبت على الراوتر لحذف كروت الوقت المنتهية</summary>
    string CleanTimeScriptName { get; }

    /// <summary>اسم الاسكريبت على الراوتر لحذف الجلسات القديمة واللوج</summary>
    string CleanSessionsScriptName { get; }

    // ── Script Content Builders ────────────────────────────────────────────

    /// <summary>
    /// يبني محتوى اسكريبت RouterOS لحذف الكروت التي استنفدت رصيدها (Bytes Quota).
    /// يحذف فقط المستخدمين منتهي الرصيد — لا يحذف المعطلة التي تملك رصيداً.
    /// </summary>
    string BuildCleanQuotaScript();

    /// <summary>
    /// يبني محتوى اسكريبت RouterOS لحذف الكروت المنتهية وقتها (Uptime Quota).
    /// </summary>
    string BuildCleanTimeScript();

    /// <summary>
    /// يبني محتوى اسكريبت RouterOS لحذف الجلسات المنتهية واللوج القديم.
    /// </summary>
    string BuildCleanSessionsScript();
}
