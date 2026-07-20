using System.Collections.Immutable;
using MathComposer.Core;

namespace MathComposer.Avalonia.Controls;

/// <summary>Describes a normalized equation submission.</summary>
public sealed class MathSubmittedEventArgs : EventArgs
{
    /// <summary>Initializes the event payload.</summary>
    public MathSubmittedEventArgs(
        string unicodeMath,
        ImmutableArray<MathDiagnostic> diagnostics)
    {
        UnicodeMath = unicodeMath ?? throw new ArgumentNullException(nameof(unicodeMath));
        Diagnostics = diagnostics.IsDefault ? [] : diagnostics;
    }

    /// <summary>Gets canonical UnicodeMath for the submitted document.</summary>
    public string UnicodeMath { get; }

    /// <summary>Gets diagnostics current at submission time.</summary>
    public ImmutableArray<MathDiagnostic> Diagnostics { get; }
}
