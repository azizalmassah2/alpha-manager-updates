namespace Lux.Management.Console.Core.Security.Diagnostics;

/// <summary>
/// واجهة كشف أدوات الهندسة العكسية والتنقيح وحقن الأكواد.
/// </summary>
public interface IAntiTamperService
{
    /// <summary>كشف وجود أي مصحح أخطاء (Managed أو Native)</summary>
    bool DetectDebugger();

    /// <summary>التحقق من سلامة التجميعات المحملة بالذاكرة وتطابقها مع الملفات الأصلية</summary>
    bool VerifyLoadedAssemblies();

    /// <summary>إخفاء خيط التنفيذ الحالي عن مصححات الأخطاء لتعطيل نقاط التوقف</summary>
    void HideCurrentThread();

    /// <summary>تنفيذ الإغلاق الطارئ الصامت في حال اكتشاف التلاعب</summary>
    void TriggerEmergencyShutdown();
}
