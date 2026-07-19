using System;
using System.Security.Cryptography;
using System.Text;
using Lux.Platform.Abstractions.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class DpapiSecureStorageService : ISecureStorageService
{
    private readonly byte[] _entropy = Encoding.UTF8.GetBytes("LuxPlatform_DPAPI_Entropy_2026");

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return encryptedText;

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            // Decryption failed (e.g. wrong user, corrupted data)
            return string.Empty;
        }
    }
}
