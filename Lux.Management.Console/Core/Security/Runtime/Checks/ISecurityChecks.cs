namespace Lux.Management.Console.Core.Security.Runtime.Checks;

/// <summary>
/// واجهة الفحص الأمني القابل للتنفيذ — تتيح إضافة فحوصات جديدة دون تعديل RuntimeMonitor.
/// </summary>
public interface ISecurityCheck
{
    /// <summary>اسم الفحص لأغراض التسجيل والتدقيق</summary>
    string CheckName { get; }

    /// <summary>هل يمكن تشغيل هذا الفحص عند مستوى المراقبة المحدد؟</summary>
    bool CanRun(int level);

    /// <summary>تنفيذ الفحص — يرمي استثناء عند اكتشاف تهديد</summary>
    void Execute();
}

/// <summary>معيار فحص مصحح الأخطاء</summary>
public interface IDebuggerCheck : ISecurityCheck { }

/// <summary>معيار فحص سلامة التجميعات المحملة</summary>
public interface IIntegrityCheck : ISecurityCheck { }

/// <summary>معيار فحص صحة توكن الجلسة</summary>
public interface ISessionValidationCheck : ISecurityCheck { }

/// <summary>معيار فحص محاولات التلاعب بالعملية</summary>
public interface ITamperCheck : ISecurityCheck { }
