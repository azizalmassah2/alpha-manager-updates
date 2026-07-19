using System.Security.Cryptography;

namespace Lux.Management.Console.Core.Security.Session;

/// <summary>
/// فئة داخلية مسؤولة عن توليد واشتقاق مفاتيح التشفير الفريدة للجلسة بالاعتماد على سر التشغيل العشوائي.
/// </summary>
public sealed class SessionKeyGenerator
{
    // مفتاح الأمان الرئيسي السري، يتولد بشكل عشوائي عند كل تشغيل للتطبيق بالذاكرة فقط
    private static readonly byte[] MasterStartupSecret = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// اشتقاق مفتاح جلسة فريد (Unique Session Key)
    /// </summary>
    public byte[] DeriveKey(byte[] nonce)
    {
        using var hmac = new HMACSHA256(MasterStartupSecret);
        return hmac.ComputeHash(nonce);
    }
}
