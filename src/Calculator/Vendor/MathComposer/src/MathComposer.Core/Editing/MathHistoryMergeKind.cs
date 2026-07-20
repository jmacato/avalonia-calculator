namespace MathComposer.Core;

/// <summary>Identifies edit classes that may coalesce into one undo unit.</summary>
public enum MathHistoryMergeKind
{
    /// <summary>The edit starts a distinct undo unit.</summary>
    None,

    /// <summary>Adjacent uninterrupted text insertion may coalesce.</summary>
    TextInsertion,

    /// <summary>One IME composition from start through commit may coalesce.</summary>
    ImeComposition
}
