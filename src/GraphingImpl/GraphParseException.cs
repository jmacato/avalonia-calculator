namespace GraphingImpl;

public sealed class GraphParseException : Exception
{
    private const string DefaultMessage = "The graph expression could not be parsed.";

    public GraphParseException()
        : base(DefaultMessage)
    {
    }

    public GraphParseException(string? message)
        : base(message)
    {
    }

    public GraphParseException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public GraphParseException(SyntaxErrorCode code, SourceSpan span, string message)
        : base(message)
    {
        Code = code;
        Span = span;
    }

    public SyntaxErrorCode Code { get; }
    public SourceSpan Span { get; }
}
