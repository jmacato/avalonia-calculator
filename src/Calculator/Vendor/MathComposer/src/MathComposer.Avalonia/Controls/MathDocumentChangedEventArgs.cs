using MathComposer.Core;

namespace MathComposer.Avalonia.Controls;

/// <summary>Describes one externally observable document replacement.</summary>
public sealed class MathDocumentChangedEventArgs : EventArgs
{
    /// <summary>Initializes the event payload.</summary>
    public MathDocumentChangedEventArgs(MathDocument oldDocument, MathDocument newDocument)
    {
        OldDocument = oldDocument ?? throw new ArgumentNullException(nameof(oldDocument));
        NewDocument = newDocument ?? throw new ArgumentNullException(nameof(newDocument));
    }

    /// <summary>Gets the document before the operation.</summary>
    public MathDocument OldDocument { get; }

    /// <summary>Gets the internally consistent document after the operation.</summary>
    public MathDocument NewDocument { get; }
}
