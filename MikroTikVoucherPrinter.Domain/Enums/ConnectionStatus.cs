namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// حالة الاتصال بالمايكروتك
/// </summary>
public enum ConnectionStatus
{
    /// <summary>غير متصل</summary>
    Disconnected = 0,

    /// <summary>جاري الاتصال</summary>
    Connecting = 1,

    /// <summary>متصل</summary>
    Connected = 2,

    /// <summary>فشل الاتصال</summary>
    Failed = 3
}
