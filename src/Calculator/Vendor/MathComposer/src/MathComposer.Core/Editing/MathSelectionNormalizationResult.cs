using System.Collections.Immutable;

namespace MathComposer.Core;

/// <summary>A normalized selection together with any normalization diagnostics.</summary>
public sealed record MathSelectionNormalizationResult
{
    /// <summary>Initializes a normalization result.</summary>
    public MathSelectionNormalizationResult(
        MathSelection selection,
        ImmutableArray<MathDiagnostic> diagnostics)
    {
        Selection = selection;
        Diagnostics = diagnostics.IsDefault ? [] : diagnostics;
    }

    /// <summary>Gets the valid directional selection.</summary>
    public MathSelection Selection { get; }

    /// <summary>Gets diagnostics raised while correcting invalid endpoints.</summary>
    public ImmutableArray<MathDiagnostic> Diagnostics { get; }
}
