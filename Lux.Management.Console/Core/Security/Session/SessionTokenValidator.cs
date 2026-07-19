using System;
using System.Security.Cryptography;
using System.Text;
using Lux.Management.Console.Core.Security.Configuration;

namespace Lux.Management.Console.Core.Security.Session;

/// <summary>
/// فئة داخلية مسؤولة عن تفكيك والتحقق من صحة وصلاحية توكنات الجلسة الرقمية.
/// </summary>
public sealed class SessionTokenValidator
{
    private readonly SessionKeyGenerator _keyGenerator;

    public SessionTokenValidator(SessionKeyGenerator keyGenerator)
    {
        _keyGenerator = keyGenerator;
    }

    public bool ValidateToken(string token, string routerSerial, out bool isPro)
    {
        isPro = false;
        if (string.IsNullOrEmpty(token)) return false;

        byte[] tokenRawBytes = null!;
        byte[] expectedSignatureBytes = null!;
        byte[] parsedSigBytes = null!;

        try
        {
            tokenRawBytes = Convert.FromBase64String(token);
            var tokenRaw = Encoding.UTF8.GetString(tokenRawBytes);

            var colonIndex = tokenRaw.LastIndexOf(':');
            if (colonIndex <= 0) return false;

            var payload = tokenRaw[..colonIndex];
            var signature = tokenRaw[(colonIndex + 1)..];

            var parts = payload.Split('|');
            if (parts.Length != 5) return false;

            var tokenRouterSerial = parts[1];
            var tokenIsPro = bool.Parse(parts[2]);
            var tokenIssuedAt = DateTime.Parse(parts[3]);
            var nonceBase64 = parts[4];

            if (tokenRouterSerial != routerSerial) return false;

            // تحقق من الصلاحية الزمنية للجلسة (تستخدم كـ Safety fallback فقط)
            if (DateTime.UtcNow - tokenIssuedAt > TimeSpan.FromHours(SecurityConfiguration.DefaultSessionTimeoutHours))
            {
                return false;
            }

            var nonce = Convert.FromBase64String(nonceBase64);
            var derivedKey = _keyGenerator.DeriveKey(nonce);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            try
            {
                using var hmac = new HMACSHA256(derivedKey);
                expectedSignatureBytes = hmac.ComputeHash(payloadBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(derivedKey);
                CryptographicOperations.ZeroMemory(nonce);
                CryptographicOperations.ZeroMemory(payloadBytes);
            }

            var expectedSignature = Convert.ToBase64String(expectedSignatureBytes);
            
            expectedSignatureBytes = Encoding.UTF8.GetBytes(expectedSignature);
            parsedSigBytes = Encoding.UTF8.GetBytes(signature);

            // مقارنة التوقيع في زمن ثابت لتلافي الهجمات الزمنية
            if (!CryptographicOperations.FixedTimeEquals(parsedSigBytes, expectedSignatureBytes))
            {
                return false;
            }

            isPro = tokenIsPro;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (tokenRawBytes != null) CryptographicOperations.ZeroMemory(tokenRawBytes);
            if (expectedSignatureBytes != null) CryptographicOperations.ZeroMemory(expectedSignatureBytes);
            if (parsedSigBytes != null) CryptographicOperations.ZeroMemory(parsedSigBytes);
        }
    }
}
