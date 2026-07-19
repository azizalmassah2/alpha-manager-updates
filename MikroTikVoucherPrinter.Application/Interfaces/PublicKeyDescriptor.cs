namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// واصف المفتاح العام الرقمي الذي يحتوي على معلومات المفتاح والخورازمية والإصدار لتسهيل التحديث المستقبلي.
/// </summary>
public class PublicKeyDescriptor
{
    public string PublicKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "RSA";
    public int Version { get; set; } = 1;
}
