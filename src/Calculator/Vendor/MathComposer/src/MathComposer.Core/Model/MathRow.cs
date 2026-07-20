using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Core;

/// <summary>An ordered, immutable sequence of math nodes.</summary>
public sealed record MathRow : MathNode
{
    /// <summary>Initializes a row from an immutable node sequence.</summary>
    /// <param name="children">The row children.</param>
    public MathRow(ImmutableArray<MathNode> children)
    {
        Children = children.IsDefault ? ImmutableArray<MathNode>.Empty : children;
        foreach (MathNode? child in Children)
        {
            ArgumentNullException.ThrowIfNull(child, nameof(children));
        }
    }

    /// <summary>Initializes a row from a node sequence.</summary>
    /// <param name="children">The row children.</param>
    public MathRow(IEnumerable<MathNode> children)
        : this((children ?? throw new ArgumentNullException(nameof(children))).ToImmutableArray())
    {
    }

    /// <summary>Gets an empty row.</summary>
    public static MathRow Empty { get; } = new(ImmutableArray<MathNode>.Empty);

    /// <summary>Gets the ordered children.</summary>
    public ImmutableArray<MathNode> Children { get; }

    /// <inheritdoc />
    public bool Equals(MathRow? other) =>
        ReferenceEquals(this, other) ||
        (other is not null && Children.SequenceEqual(other.Children));

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (MathNode child in Children)
        {
            hash.Add(child);
        }

        return hash.ToHashCode();
    }
}
