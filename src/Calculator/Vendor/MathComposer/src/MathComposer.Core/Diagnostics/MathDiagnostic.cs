namespace MathComposer.Core;

/// <summary>A stable diagnostic produced by import, editing, or layout.</summary>
public sealed record MathDiagnostic
{
    /// <summary>Initializes a diagnostic.</summary>
    public MathDiagnostic(
        string code,
        MathDiagnosticSeverity severity,
        string message,
        MathTextFormat sourceFormat,
        MathSourceSpan? sourceSpan = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(message);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        if (!Enum.IsDefined(sourceFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceFormat));
        }

        Code = code;
        Severity = severity;
        Message = message;
        SourceFormat = sourceFormat;
        SourceSpan = sourceSpan;
    }

    /// <summary>Gets the stable diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets the diagnostic severity.</summary>
    public MathDiagnosticSeverity Severity { get; }

    /// <summary>Gets the human-readable message.</summary>
    public string Message { get; }

    /// <summary>Gets the related source format.</summary>
    public MathTextFormat SourceFormat { get; }

    /// <summary>Gets the optional original UTF-16 source span.</summary>
    public MathSourceSpan? SourceSpan { get; }
}
