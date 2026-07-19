using System;
using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// خدمة التحقق من الترخيص — تعتمد على ملف license.dat الموقَّع بـ RSA
/// من برنامج LicenseGenerator (YAZ-WART)
/// </summary>
public interface ILicenseService
{
    /// <summary>هل يوجد ملف license.dat في مجلد البرنامج؟</summary>
    bool HasStoredLicense();

    /// <summary>
    /// التحقق الكامل من صحة ملف الترخيص.
    /// (المعاملات routerIp و apiPassword محتفَظ بها للتوافق — غير مستخدمة في هذا النظام)
    /// </summary>
    Task<LicenseStatus> CheckLicenseAsync(string routerIp = "", string apiPassword = "", CancellationToken ct = default);

    /// <summary>
    /// تفعيل البرنامج بملف license.dat الصادر من المشرف.
    /// (المعامل licenseFilePath = المسار الكامل لملف license.dat)
    /// </summary>
    Task<bool> ActivateAsync(string routerIp, string apiPassword, string licenseFilePath, CancellationToken ct = default);

    /// <summary>حذف ملف الترخيص وإعادة الضبط</summary>
    void Revoke();

    /// <summary>التحقق من الترخيص بشكل غير متصل بالإنترنت باستخدام الرقم التسلسلي للراوتر</summary>
    LicenseStatus ValidateLicenseOffline(string serialNumber);

    /// <summary>تفاصيل تشخيص آخر محاولة تفعيل أو فحص للترخيص</summary>
    string LastDiagnostics { get; }
}

/// <summary>حالة الترخيص</summary>
public enum LicenseStatus
{
    /// <summary>الترخيص صالح</summary>
    Valid,

    /// <summary>الجهاز الحالي لا يطابق الترخيص</summary>
    InvalidRouter,

    /// <summary>لا يوجد ملف ترخيص — يجب التفعيل</summary>
    NotActivated,

    /// <summary>لا يمكن الاتصال (غير مستخدم في هذا النظام)</summary>
    CannotConnect,

    /// <summary>ملف الترخيص تالف أو التوقيع غير صالح أو انتهت الصلاحية</summary>
    Corrupted
}
