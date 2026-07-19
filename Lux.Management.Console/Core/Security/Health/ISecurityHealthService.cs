using Lux.Management.Console.Core.Security.Models;

namespace Lux.Management.Console.Core.Security.Health;

/// <summary>
/// واجهة خدمة التشخيص لمؤشرات الأمان الحيوية (مخصصة للتشخيص فقط، ولا تُستخدم للتفويض).
/// </summary>
public interface ISecurityHealthService
{
    /// <summary>الحصول على لقطة تشخيصية فورية لسلامة النظام الأمني بالذاكرة</summary>
    SecurityHealthSnapshot GetHealthSnapshot();
}
