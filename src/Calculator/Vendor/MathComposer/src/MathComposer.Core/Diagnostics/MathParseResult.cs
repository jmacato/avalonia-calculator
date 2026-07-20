using System.Collections.Immutable;

namespace MathComposer.Core;

/// <summary>A recovered document and its import diagnostics.</summary>
public sealed record MathParseResult
{
    /// <summary>Initializes a parse result.</summary>
    public MathParseResult(MathDocument document, ImmutableArray<MathDiagnostic> diagnostics)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Diagnostics = diagnostics.IsDefault ? ImmutableArray<MathDiagnostic>.Empty : diagnostics;
        if (Diagnostics.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Diagnostics cannot contain null entries.", nameof(diagnostics));
        }
    }

    /// <summary>Gets the recovered document.</summary>
    public MathDocument Document { get; }

    /// <summary>Gets ordered diagnostics.</summary>
    public ImmutableArray<MathDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether any diagnostic is fatal.</summary>
    public bool HasFatalDiagnostics =>
        Diagnostics.Any(static diagnostic => diagnostic.Severity == MathDiagnosticSeverity.Fatal);
}
