namespace CalcEngine;

public class CalcErrException : Exception
{
    public override string Message { get; }
    internal CalcErr err { get; }

    public CalcErrException(CalcErr _err)
    {
        err = _err;
        Message = $"CalcError {Enum.GetName(err)}";
    }
}
