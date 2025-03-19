namespace CalcManagerPort;

public class CalcErrException : Exception
{
    public override string Message { get; }

    public CalcErrException(CalcErr err)
    {
        Message = $"CalcError {Enum.GetName(err)}";
    }
}
