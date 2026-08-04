namespace Lux.Management.Console.Core.Security.Models;

/// <summary>
/// معرفات الميزات والوظائف المحددة بقوة لبرنامج Alpha Manager.
/// </summary>
public enum FeatureId
{
    /// <summary>إدارة الراوتر وتفاصيله (Router Management)</summary>
    RouterManagement,

    /// <summary>أجهزة البث وإدارتها (Broadcasting)</summary>
    Broadcasting,

    /// <summary>مراقبة الشبكة والفيلانات (Network Monitoring)</summary>
    NetworkMonitoring,

    /// <summary>توليد كروت الهوتسبوت (Voucher Generation)</summary>
    VoucherGeneration,

    /// <summary>طباعة وتصدير الكروت (Voucher Printing)</summary>
    VoucherPrinting,

    /// <summary>إدارة الفيلانات (Vlan Management)</summary>
    VlanManagement,

    /// <summary>تعديل ورفع صفحات تسجيل الدخول للهوتسبوت (Hotspot Login Editor)</summary>
    HotspotLoginEditor,

    /// <summary>سجلات الأخطاء المتقدمة والتدقيق (Advanced Logs)</summary>
    AdvancedLogs,

    /// <summary>صيانة النظام والراوتر وإعادة بناء قاعدة البيانات (System Maintenance)</summary>
    SystemMaintenance
}
