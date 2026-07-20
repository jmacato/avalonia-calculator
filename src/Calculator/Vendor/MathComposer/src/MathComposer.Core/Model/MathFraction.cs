using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Core;

/// <summary>A numerator over a denominator.</summary>
public sealed record MathFraction : MathNode
{
    /// <summary>Initializes a fraction.</summary>
    public MathFraction(MathRow numerator, MathRow denominator)
    {
        Numerator = numerator ?? throw new ArgumentNullException(nameof(numerator));
        Denominator = denominator ?? throw new ArgumentNullException(nameof(denominator));
    }

    /// <summary>Gets the numerator row.</summary>
    public MathRow Numerator { get; }

    /// <summary>Gets the denominator row.</summary>
    public MathRow Denominator { get; }
}
