namespace MathComposer.Core;

/// <summary>A directional selection with anchor and active endpoints.</summary>
public readonly record struct MathSelection(MathPosition Anchor, MathPosition Active)
{
    /// <summary>Gets whether the selection is a caret.</summary>
    public bool IsCollapsed => Anchor == Active;
}
