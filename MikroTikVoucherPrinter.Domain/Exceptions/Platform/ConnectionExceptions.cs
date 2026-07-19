using System;

namespace MikroTikVoucherPrinter.Domain.Exceptions.Platform;

public class ConnectionFailedException : Exception
{
    public ConnectionFailedException(string message) : base(message) { }
    public ConnectionFailedException(string message, Exception innerException) : base(message, innerException) { }
}

public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message) : base(message) { }
    public AuthenticationFailedException(string message, Exception innerException) : base(message, innerException) { }
}

public class DeviceCommunicationException : Exception
{
    public DeviceCommunicationException(string message) : base(message) { }
    public DeviceCommunicationException(string message, Exception innerException) : base(message, innerException) { }
}
