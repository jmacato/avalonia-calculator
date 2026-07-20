using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Core;

/// <summary>A base row with optional content below and above it.</summary>
public sealed record MathUnderOver : MathNode
{
    /// <summary>Initializes a validated under/over node.</summary>
    public MathUnderOver(MathRow @base, MathRow? below, MathRow? above, MathUnderOverKind kind)
    {
        Base = @base ?? throw new ArgumentNullException(nameof(@base));
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (below is null && above is null && kind == MathUnderOverKind.NaryLimits)
        {
            throw new ArgumentException("N-ary limits require content below, above, or both.");
        }

        if (kind == MathUnderOverKind.NaryLimits &&
            (@base.Children.Length != 1 ||
             @base.Children[0] is not MathText { Text: "∑" or "∏" or "∐" or "∫" or "∬" or "∭" }))
        {
            throw new ArgumentException("N-ary limits require one supported n-ary operator as their base.", nameof(@base));
        }

        Below = below;
        Above = above;
        Kind = kind;
    }

    /// <summary>Gets the base row.</summary>
    public MathRow Base { get; }

    /// <summary>Gets the optional row below the base.</summary>
    public MathRow? Below { get; }

    /// <summary>Gets the optional row above the base.</summary>
    public MathRow? Above { get; }

    /// <summary>Gets the construction kind.</summary>
    public MathUnderOverKind Kind { get; }
}
