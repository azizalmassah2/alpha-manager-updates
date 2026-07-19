using System;
using System.Security.Cryptography;
using System.Text;

namespace Lux.Management.Console.Core.Security.Session;

/// <summary>
/// فئة داخلية مسؤولة عن بناء وتوقيع توكن الجلسة.
/// </summary>
public sealed class SessionTokenFactory
{
    private readonly SessionKeyGenerator _keyGenerator;

    public SessionTokenFactory(SessionKeyGenerator keyGenerator)
    {
        _keyGenerator = keyGenerator;
    }

    public string CreateToken(string routerSerial, bool isPro)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var issuedAt = DateTime.UtcNow.ToString("o");
        var nonce = RandomNumberGenerator.GetBytes(16);
        var nonceBase64 = Convert.ToBase64String(nonce);

        var payload = $"{sessionId}|{routerSerial}|{isPro}|{issuedAt}|{nonceBase64}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var derivedKey = _keyGenerator.DeriveKey(nonce);
        byte[] signatureBytes;

        try
        {
            using var hmac = new HMACSHA256(derivedKey);
            signatureBytes = hmac.ComputeHash(payloadBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
            CryptographicOperations.ZeroMemory(nonce);
        }

        var signature = Convert.ToBase64String(signatureBytes);
        CryptographicOperations.ZeroMemory(signatureBytes);

        var tokenRaw = $"{payload}:{signature}";
        var tokenRawBytes = Encoding.UTF8.GetBytes(tokenRaw);
        
        try
        {
            return Convert.ToBase64String(tokenRawBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payloadBytes);
            CryptographicOperations.ZeroMemory(tokenRawBytes);
        }
    }
}
