using System;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace Lux.Management.Console.Core.Security.Trust;

/// <summary>
/// فئة التحقق المعزولة لهوية الراوتر واتساق الترخيص والمطابقة الرقمية.
/// </summary>
public sealed class RouterTrustVerifier
{
    private readonly IPublicKeyProvider _publicKeyProvider;

    public RouterTrustVerifier(IPublicKeyProvider publicKeyProvider)
    {
        _publicKeyProvider = publicKeyProvider;
    }

    /// <summary>
    /// التحقق من مطابقة سريال الراوتر وتوقيع الترخيص وصحة المفتاح العام.
    /// </summary>
    public bool VerifyRouterTrust(byte[] data, byte[] signature, string currentHwid, string licenseHwid)
    {
        if (!_publicKeyProvider.VerifyPublicKeyIntegrity())
        {
            return false;
        }

        if (!_publicKeyProvider.VerifySignature(data, signature))
        {
            return false;
        }

        return string.Equals(currentHwid, licenseHwid, StringComparison.OrdinalIgnoreCase);
    }
}
