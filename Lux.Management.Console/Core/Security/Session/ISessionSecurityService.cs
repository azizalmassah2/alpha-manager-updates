namespace Lux.Management.Console.Core.Security.Session;

/// <summary>
/// واجهة إدارة أمان الجلسة وتوليد والتحقق من رموز HMAC للجلسات بالذاكرة.
/// </summary>
public interface ISessionSecurityService
{
    /// <summary>توليد رمز أمان فريد وموقع للجلسة الجديدة للراوتر المحدد</summary>
    string GenerateSessionToken(string routerSerial, bool isPro);

    /// <summary>التحقق من صحة وسلامة رمز الجلسة والتاكد من عدم تلاعبه</summary>
    bool ValidateSessionToken(string token, string routerSerial, out bool isPro);
}
