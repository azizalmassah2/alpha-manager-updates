using System;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// معلومات الراوتر المسترجعة بعد الاتصال الناجح
/// </summary>
public class RouterInfo
{
    /// <summary>معرف الجهاز الفريد (من قاعدة البيانات)</summary>
    public Guid RouterId { get; set; }

    /// <summary>عنوان IP أو الاسم المضيف</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>المنفذ المستخدم للاتصال</summary>
    public int Port { get; set; } = 8728;

    /// <summary>اسم المستخدم</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>اسم الجهاز (هوية RouterOS)</summary>
    public string Identity { get; set; } = string.Empty;

    /// <summary>الرقم التسلسلي للجهاز</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Software ID من RouterOS</summary>
    public string SoftwareId { get; set; } = string.Empty;

    /// <summary>إصدار RouterOS</summary>
    public string RouterOsVersion { get; set; } = string.Empty;

    /// <summary>نموذج اللوحة</summary>
    public string BoardModel { get; set; } = string.Empty;

    /// <summary>بنية المعالج</summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>وقت الاتصال</summary>
    public DateTime ConnectedAt { get; set; } = DateTime.Now;

    public string DisplayName => string.IsNullOrWhiteSpace(Identity) ? Host : Identity;
}
