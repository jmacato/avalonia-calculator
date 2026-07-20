namespace MathComposer.Core;

/// <summary>A document and selection snapshot restored by undo or redo.</summary>
public sealed record MathHistoryState
{
    /// <summary>Initializes a history snapshot.</summary>
    public MathHistoryState(MathDocument document, MathSelection selection)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Selection = selection;
    }

    /// <summary>Gets the immutable document snapshot.</summary>
    public MathDocument Document { get; }

    /// <summary>Gets the directional selection snapshot.</summary>
    public MathSelection Selection { get; }
}
