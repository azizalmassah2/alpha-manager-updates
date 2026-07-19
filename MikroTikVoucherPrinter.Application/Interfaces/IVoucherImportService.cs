using System;
using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// واجهة خدمة استيراد الكروت المسبقة من الراوتر لقاعدة البيانات المحلية
/// </summary>
public interface IVoucherImportService
{
    /// <summary>
    /// التحقق مما إذا كان الراوتر يحتوي على كروت غير مستوردة محلياً (شرط التشغيل الأول)
    /// </summary>
    Task<bool> IsImportRequiredAsync(Guid routerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// استيراد الكروت من الراوتر بالخلفية وبصيغة دفقية مصفحة مع تحديث تقدم المعالجة
    /// </summary>
    Task ImportVouchersAsync(Guid routerId, Action<int, int> progressCallback, CancellationToken cancellationToken = default);
}
