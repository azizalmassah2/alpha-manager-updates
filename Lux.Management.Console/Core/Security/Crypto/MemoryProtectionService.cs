using System;
using System.Security.Cryptography;
using System.Text;

namespace Lux.Management.Console.Core.Security.Crypto;

/// <summary>
/// تطبيق IMemoryProtectionService باستخدام تشفير DPAPI على مستوى نظام التشغيل للويندوز.
/// </summary>
public class MemoryProtectionService : IMemoryProtectionService
{
    public string ProtectString(string rawData, byte[] entropy)
    {
        if (string.IsNullOrEmpty(rawData)) return string.Empty;

        try
        {
            var rawBytes = Encoding.UTF8.GetBytes(rawData);
            
            // تشفير البيانات باستخدام DPAPI ونطاق المستخدم الحالي للويندوز (CurrentUser)
            var protectedBytes = ProtectedData.Protect(rawBytes, entropy, DataProtectionScope.CurrentUser);
            
            return Convert.ToBase64String(protectedBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    public string UnprotectString(string encryptedData, byte[] entropy)
    {
        if (string.IsNullOrEmpty(encryptedData)) return string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedData);
            
            // فك التشفير باستخدام DPAPI
            var rawBytes = ProtectedData.Unprotect(encryptedBytes, entropy, DataProtectionScope.CurrentUser);
            
            return Encoding.UTF8.GetString(rawBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
