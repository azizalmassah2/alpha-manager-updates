using Lux.Management.Console.Core.Session;

namespace Lux.Management.Console.Core.Security.Context;

/// <summary>
/// واجهة تحديث سياق الأمان الداخلي - تُستعمل فقط من قبل خدمات المصادقة والمراقب ورصد التلاعب.
/// </summary>
public interface ISecurityContextUpdater
{
    /// <summary>تهيئة سياق الأمان بجلسة جديدة</summary>
    void Initialize(ApplicationSession session);

    /// <summary>إبطال سياق الأمان وتخفيضه للوضع المجاني فوراً لأسباب أمنية</summary>
    void Invalidate();
}
