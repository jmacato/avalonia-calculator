namespace MathComposer.Core;

/// <summary>A square or indexed radical.</summary>
public sealed record MathRadical : MathNode
{
    /// <summary>Initializes a square or indexed radical.</summary>
    public MathRadical(MathRow radicand, MathRow? degree = null)
    {
        Radicand = radicand ?? throw new ArgumentNullException(nameof(radicand));
        Degree = degree;
    }

    /// <summary>Gets the radicand.</summary>
    public MathRow Radicand { get; }

    /// <summary>Gets the optional degree.</summary>
    public MathRow? Degree { get; }
}
