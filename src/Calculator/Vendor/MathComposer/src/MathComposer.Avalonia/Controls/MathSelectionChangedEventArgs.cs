using System.Collections.Immutable;
using MathComposer.Core;

namespace MathComposer.Avalonia.Controls;

/// <summary>Describes one externally observable selection replacement.</summary>
public sealed class MathSelectionChangedEventArgs : EventArgs
{
    /// <summary>Initializes the event payload.</summary>
    public MathSelectionChangedEventArgs(MathSelection oldSelection, MathSelection newSelection)
    {
        OldSelection = oldSelection;
        NewSelection = newSelection;
    }

    /// <summary>Gets the previous directional selection.</summary>
    public MathSelection OldSelection { get; }

    /// <summary>Gets the new normalized directional selection.</summary>
    public MathSelection NewSelection { get; }
}
