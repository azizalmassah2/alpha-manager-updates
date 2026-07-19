namespace MikroTikVoucherPrinter.Domain.Enums.Platform;

public enum ConnectionState
{
    Unknown,
    Disconnected,
    Connecting,
    Connected,
    Switching,
    AuthenticationFailed,
    Timeout,
    Error
}
