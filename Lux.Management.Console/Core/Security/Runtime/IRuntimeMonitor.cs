using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.Management.Console.Core.Session;

namespace Lux.Management.Console.Core.Security.Runtime;

/// <summary>
/// واجهة خيط المراقبة الأمنية كخدمة مستضافة (Hosted Service) مع دورة حياة مهيأة أمنياً.
/// </summary>
public interface IRuntimeMonitor : IDisposable
{
    /// <summary>بدء مراقبة الجلسة والبرنامج في الخلفية</summary>
    Task StartAsync(ApplicationSession session, CancellationToken cancellationToken = default);

    /// <summary>إيقاف خيط المراقبة</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>المستوى 1: تشغيل الفحوصات الأمنية الفورية (المدفوعة بالحدث)</summary>
    void ExecuteLevel1Checks();

    /// <summary>المستوى 3: تشغيل الفحوصات المكثفة للسلامة (عند الطلب)</summary>
    void ExecuteLevel3Checks();
}
