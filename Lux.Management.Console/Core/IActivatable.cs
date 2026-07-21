namespace Lux.Management.Console.Core;

/// <summary>
/// واجهة تُطبَّق على ViewModels التي تحمّل بياناتها عند التنقل إليها فعلياً.
/// يمنع تحميل البيانات في Constructor ويؤجله إلى لحظة الاختيار الفعلي.
/// </summary>
public interface IActivatable
{
    /// <summary>
    /// يُستدعى مرة واحدة عند أول تنقل للشاشة، ثم عند كل إعادة تفعيل.
    /// </summary>
    Task ActivateAsync();
}

/// <summary>
/// واجهة تُطبَّق على ViewModels التي تحتاج إلى إيقاف موارد عند مغادرة الشاشة.
/// مثال: إيقاف timer polling عند الانتقال لشاشة أخرى.
/// </summary>
public interface IDeactivatable
{
    /// <summary>
    /// يُستدعى عند مغادرة الشاشة — لإيقاف الـ polling أو تحرير الموارد.
    /// </summary>
    void Deactivate();
}
