namespace MathComposer.Core;

/// <summary>The severity of a parse, edit, or layout diagnostic.</summary>
public enum MathDiagnosticSeverity
{
    /// <summary>Informational feedback.</summary>
    Info,

    /// <summary>A non-fatal warning.</summary>
    Warning,

    /// <summary>A recoverable error.</summary>
    Error,

    /// <summary>A resource or safety failure that rejects the remaining input.</summary>
    Fatal
}
