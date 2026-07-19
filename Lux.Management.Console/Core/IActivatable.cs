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
