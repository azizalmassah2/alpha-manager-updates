using Lux.Management.Console.Core.Security.Models;

namespace Lux.Management.Console.Core.Security.Authorization;

/// <summary>
/// واجهة خدمة التحقق من صلاحية الوصول واستخدام الميزات المحددة بقوة.
/// </summary>
public interface IFeatureAuthorizationService
{
    /// <summary>التحقق من تفعيل ميزة معينة للترخيص النشط</summary>
    bool HasFeature(FeatureId featureId);

    /// <summary>فرض توفر ميزة معينة ورمي استثناء في حال غيابها</summary>
    void RequireFeature(FeatureId featureId);

    /// <summary>التحقق من إمكانية تنفيذ عملية ما تحت سياق محدد (كفحص الحدود المسموحة)</summary>
    bool CanExecute(FeatureId featureId, object context);
}
