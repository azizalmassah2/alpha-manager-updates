namespace Lux.Management.Console.Core.Security.Crypto;

/// <summary>
/// واجهة خدمة التشفير وحماية البيانات بالذاكرة محلياً باستخدام DPAPI (Windows Data Protection API).
/// </summary>
public interface IMemoryProtectionService
{
    /// <summary>تشفير سلسلة نصية بناءً على سر عشوائي ونطاق المستخدم</summary>
    string ProtectString(string rawData, byte[] entropy);

    /// <summary>فك تشفير سلسلة نصية مشفرة بـ DPAPI</summary>
    string UnprotectString(string encryptedData, byte[] entropy);
}
