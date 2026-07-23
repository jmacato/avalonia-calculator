namespace MathComposer.Core;

/// <summary>An accent placed over or under a base row.</summary>
public sealed record MathAccent : MathNode
{
    /// <summary>Initializes an accent.</summary>
    public MathAccent(
        MathRow @base,
        MathAccentKind kind,
        MathAccentPlacement placement = MathAccentPlacement.Over)
    {
        Base = @base ?? throw new ArgumentNullException(nameof(@base));
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Enum.IsDefined(placement))
        {
            throw new ArgumentOutOfRangeException(nameof(placement));
        }

        Kind = kind;
        Placement = placement;
    }

    /// <summary>Gets the base row.</summary>
    public MathRow Base { get; }

    /// <summary>Gets the accent kind.</summary>
    public MathAccentKind Kind { get; }

    /// <summary>Gets whether the accent is over or under the base.</summary>
    public MathAccentPlacement Placement { get; }
}
