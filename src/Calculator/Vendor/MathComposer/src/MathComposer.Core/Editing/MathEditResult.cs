using System.Collections.Immutable;

namespace MathComposer.Core;

/// <summary>The immutable outcome of one pure editing command.</summary>
public sealed record MathEditResult
{
    /// <summary>Initializes an edit result.</summary>
    public MathEditResult(
        MathDocument document,
        MathSelection selection,
        ImmutableArray<MathDiagnostic> diagnostics,
        MathSelection? inputRegion = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Selection = selection;
        Diagnostics = diagnostics.IsDefault ? [] : diagnostics;
        InputRegion = inputRegion;
    }

    /// <summary>Gets the edited document.</summary>
    public MathDocument Document { get; }

    /// <summary>Gets the remapped directional selection.</summary>
    public MathSelection Selection { get; }

    /// <summary>Gets diagnostics raised by the command.</summary>
    public ImmutableArray<MathDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the transient linear-input region that remains pending after this edit.
    /// A null value means that substitution or structural build-up completed it.
    /// </summary>
    public MathSelection? InputRegion { get; }
}
