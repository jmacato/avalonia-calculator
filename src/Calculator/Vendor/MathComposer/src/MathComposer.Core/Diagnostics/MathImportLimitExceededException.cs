namespace MathComposer.Core;

/// <summary>Represents a bounded import that exceeded a parser resource limit.</summary>
public sealed class MathImportLimitExceededException : Exception
{
    /// <summary>Initializes an exception without a diagnostic message.</summary>
    public MathImportLimitExceededException()
    {
    }

    /// <summary>Initializes an exception with a diagnostic message.</summary>
    public MathImportLimitExceededException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes an exception with a diagnostic message and inner exception.</summary>
    public MathImportLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal MathImportLimitExceededException(
        string code,
        string message,
        MathSourceSpan span)
        : base(message)
    {
        Code = code;
        Span = span;
    }

    internal string Code { get; } = string.Empty;

    internal MathSourceSpan Span { get; }
}
