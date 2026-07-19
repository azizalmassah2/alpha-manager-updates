using System.Threading;
using System.Threading.Tasks;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// خدمة الاتصال المركزية — تنسيق عملية الاتصال بالراوتر، التحقق من الترخيص، وبناء الجلسة
/// </summary>
public interface IConnectionService
{
    /// <summary>محاولة الاتصال بالراوتر وجلب تفاصيله وتعيينه كراوتر نشط</summary>
    Task<RouterConnectionResult> ConnectAsync(string host, int port, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>التحقق من صحة الترخيص ومطابقته للراوتر المتصل</summary>
    Task<LicenseVerificationResult> VerifyLicenseAsync(RouterInfo routerInfo, CancellationToken cancellationToken = default);

    /// <summary>بناء كائن الجلسة الفعلي بناءً على نتائج الاتصال والترخيص</summary>
    Task<ApplicationSession> CreateSessionAsync(RouterInfo? routerInfo, LicenseVerificationResult? licenseResult);
}
