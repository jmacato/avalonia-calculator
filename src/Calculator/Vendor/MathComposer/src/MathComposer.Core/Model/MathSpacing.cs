namespace MathComposer.Core;

/// <summary>An explicit supported mathematical space.</summary>
public sealed record MathSpacing : MathNode
{
    /// <summary>Initializes an explicit mathematical space.</summary>
    public MathSpacing(MathSpacingWidth width)
    {
        if (!Enum.IsDefined(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        Width = width;
    }

    /// <summary>Gets the canonical spacing width.</summary>
    public MathSpacingWidth Width { get; }
}
