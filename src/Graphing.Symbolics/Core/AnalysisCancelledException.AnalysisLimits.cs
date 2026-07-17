namespace Graphing.Symbolics;

internal sealed class AnalysisCancelledException : OperationCanceledException
{
    public AnalysisCancelledException()
    {
    }

    public AnalysisCancelledException(string message)
        : base(message)
    {
    }

    public AnalysisCancelledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
