using System;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Models;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// خدمة فحص التحديثات وتثبيتها
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// فحص ما إذا كان هناك تحديث متاح على السيرفر.
    ///
    /// السلوك:
    ///   - لا تُلقي استثناءً عند فشل الاتصال — تُرجع UpdateCheckResult.NoUpdate
    ///   - تراعي حقل enabled (false = تجاهل الإصدار)
    ///   - تراعي minimumSupportedVersion (أقل منه = إجباري)
    ///   - تراعي updateType و mandatory
    /// </summary>
    /// <returns>
    ///   UpdateCheckResult.NoUpdate    — لا يوجد تحديث أو فشل الاتصال
    ///   UpdateCheckResult.Available() — يوجد تحديث (راجع MustUpdate للإجبارية)
    /// </returns>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// تنزيل الملف وتشغيل المثبّت ثم إغلاق البرنامج.
    /// </summary>
    /// <param name="update">بيانات التحديث</param>
    /// <param name="progress">تقرير نسبة التنزيل (0-100)</param>
    /// <param name="ct">رمز الإلغاء</param>
    Task DownloadAndInstallAsync(UpdateInfo update, IProgress<int> progress, CancellationToken ct = default);
}
