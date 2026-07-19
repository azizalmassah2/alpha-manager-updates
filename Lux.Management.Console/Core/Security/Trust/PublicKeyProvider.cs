using System;
using System.Security.Cryptography;
using System.Text;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace Lux.Management.Console.Core.Security.Trust;

/// <summary>
/// تطبيق IPublicKeyProvider لإدارة المفتاح العام وتدقيق سلامته وحمايته من التلاعب بإعادة واصف المفتاح العام.
/// </summary>
public class PublicKeyProvider : IPublicKeyProvider
{
    private const string PublicKeyXml =
        "<RSAKeyValue><Modulus>788Ha86wzO0AYs43DxUcX9JNIsu5m2UKDO7l6xWe6DyZwPPj8d32JDVR/skZc5ELwXl5Kmh902Dlz/mq/aA+GnlD5bbNuRSAMYvmEtvpPAW81qg8pwfNRbWV1ot0vL4w2UUJZesTxXvxdNSlCA+dzk2myLw+wRPq3wCH63yG2sUTni8McfoqxWw4GO2xQZiXEaUdLX2K6g+0TZeWYtHBAawp13uW74cHzEjpWpPF4b7YoPEn3JAPRhmaUTLIHs6aMBJXTKynatkwMYGMEtGjLXDA63hyx5UUw0QnkhtjtqkDGpfdt+J1GiAvl9i2xzyjKKFByllS1vgcPdcKmucGEKV7qVEhLl6IWJB48w5eIMEEvAElMSY3gKNF7vWdH9l8SUPEG9qJsU51QXcLdeUyliNpsezXOQ05y4TqFrNyHU+J+aP54jz4nIx41vQQlDxP0fyhbffzIWc+90lGjkVzW0va7Ci2pmIhnOSH44ef+Cyn0q8bX361V9JISh+4MxrzmijEqA2IBXcvwTQc+jdpI7BckTihENVOMy9hbA64XIW/Kdn7IlVIoVn+SVI8HI00WYveni8E5vhplXIbpGgNKadiuTQH9K+6nxU5tVu2WPXFrCS3PYQFhF4p8KTdndYo4SL7oDN6oxUevtPKj4m/y66qgNMRA5jLkRJKFoTlAC0=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    private const string ExpectedHash = "8f0c43e589529bd38d5057efae390dfed4ac7df0d1ae19ce68d3c9503af4af60";

    public PublicKeyDescriptor GetPublicKeyDescriptor()
    {
        if (!VerifyPublicKeyIntegrity())
        {
            throw new CryptographicException("❌ [Security] تم العبث بالمفتاح العام للبرنامج!");
        }
        return new PublicKeyDescriptor
        {
            PublicKey = PublicKeyXml,
            Fingerprint = ExpectedHash,
            Algorithm = "RSA",
            Version = 1
        };
    }

    public bool VerifyPublicKeyIntegrity()
    {
        try
        {
            var rawBytes = Encoding.UTF8.GetBytes(PublicKeyXml);
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(rawBytes);
            
            var builder = new StringBuilder();
            foreach (var b in hashBytes)
            {
                builder.Append(b.ToString("x2"));
            }
            
            var hashStr = builder.ToString();
            return string.Equals(hashStr, ExpectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public bool VerifySignature(byte[] data, byte[] signature)
    {
        if (!VerifyPublicKeyIntegrity())
        {
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.FromXmlString(PublicKeyXml);
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }
}
