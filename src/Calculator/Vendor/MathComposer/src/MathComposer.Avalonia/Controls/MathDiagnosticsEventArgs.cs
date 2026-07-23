using System.Collections.Immutable;
using MathComposer.Core;

namespace MathComposer.Avalonia.Controls;

/// <summary>Publishes the editor's current ordered diagnostics.</summary>
public sealed class MathDiagnosticsEventArgs : EventArgs
{
    /// <summary>Initializes the event payload.</summary>
    public MathDiagnosticsEventArgs(ImmutableArray<MathDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics.IsDefault ? [] : diagnostics;
    }

    /// <summary>Gets the current diagnostics.</summary>
    public ImmutableArray<MathDiagnostic> Diagnostics { get; }
}
