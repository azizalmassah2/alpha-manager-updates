using System;

namespace Lux.MikroTik.Exceptions;

public class MikroTikCommandException : Exception
{
    public MikroTikCommandException(string message) : base(message) { }
    public MikroTikCommandException(string message, Exception innerException) : base(message, innerException) { }
}
