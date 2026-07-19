using System.Text;

namespace Lux.Management.Console.Core.Security.Configuration;

/// <summary>
/// ثوابت وإعدادات الأمان المركزية لبرنامج Alpha Manager.
/// </summary>
public static class SecurityConfiguration
{
    // الحدود المجانية الافتراضية
    public const int MaxFreeVouchersLimit = 840;

    // إعدادات وقت التشغيل والتحقق
    public const int DefaultSessionTimeoutHours = 24; // الحد الأقصى كحزام أمان احتياطي

    // إعدادات خيط المراقبة (Runtime Monitor)
    public const int Level2MinIntervalSeconds = 30; // الحد الأدنى للمراقبة الدورية الخفيفة
    public const int Level2MaxIntervalSeconds = 90; // الحد الأقصى للمراقبة الدورية الخفيفة

    // إعدادات تصفير وبصمات الذاكرة
    public static readonly byte[] AuditEntropy = Encoding.UTF8.GetBytes("LuxCard_Security_Audit_Entropy_2026");
    public static readonly byte[] SessionEntropy = Encoding.UTF8.GetBytes("LuxCard_Session_Key_Derivation_Entropy_2026");

    // تفعيل إجراءات الإغلاق الطارئ الصارم
    public const bool EnableStrictGracefulShutdown = true;
    public const int GracefulShutdownTimeoutMs = 1500; // مهلة الإغلاق التدريجي قبل فرض القتل الإجباري للعملية
}
