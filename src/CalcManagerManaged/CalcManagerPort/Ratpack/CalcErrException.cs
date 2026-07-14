namespace CalcEngine;

public class CalcErrException : Exception
{
    internal CalcErr err { get; }

    public CalcErrException(CalcErr err)
        : base($"CalcError {Enum.GetName(typeof(CalcErr), err)}")
    {
        this.err = err;
    }

    public CalcErrException()
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
