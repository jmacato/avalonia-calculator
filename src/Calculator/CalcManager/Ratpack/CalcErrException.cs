using System;

namespace CalcEngine;

public sealed class CalcErrException : Exception
{
    internal CalcErr Error { get; }

    internal CalcErrException(CalcErr error)
        : base($"CalcError {Enum.GetName(error)}")
    {
        Error = error;
    }

    public CalcErrException()
        : base("Calculator error.")
    {
    }

    public CalcErrException(string message)
        : base(message)
    {
    }

    public CalcErrException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
