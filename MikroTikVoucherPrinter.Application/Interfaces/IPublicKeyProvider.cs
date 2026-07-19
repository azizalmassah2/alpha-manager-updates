namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// واجهة توفير والتحقق من سلامة المفتاح العام RSA الخاص بالترخيص.
/// </summary>
public interface IPublicKeyProvider
{
    /// <summary>الحصول على واصف المفتاح العام بما يضمن دعم إصدارات متعددة</summary>
    PublicKeyDescriptor GetPublicKeyDescriptor();

    /// <summary>التحقق من سلامة المفتاح العام بالذاكرة لمنع محاولات استبداله بكود مخرب</summary>
    bool VerifyPublicKeyIntegrity();

    /// <summary>التحقق من صحة توقيع رقمي باستخدام المفتاح العام الخاص بالمنظومة</summary>
    bool VerifySignature(byte[] data, byte[] signature);
}
