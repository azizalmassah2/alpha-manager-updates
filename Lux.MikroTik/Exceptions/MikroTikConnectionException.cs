using System;

namespace Lux.MikroTik.Exceptions;

public class MikroTikConnectionException : Exception
{
    public MikroTikConnectionException(string message) : base(message) { }
    public MikroTikConnectionException(string message, Exception innerException) : base(message, innerException) { }
}
