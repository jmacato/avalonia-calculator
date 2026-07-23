using System.Collections.Immutable;

namespace MathComposer.Core;

/// <summary>A structural path and UTF-16 offset within a math document.</summary>
public readonly record struct MathPosition
{
    /// <summary>Initializes a position.</summary>
    public MathPosition(ImmutableArray<int> path, int offset)
    {
        Path = path.IsDefault ? ImmutableArray<int>.Empty : path;
        Offset = offset;
    }

    /// <summary>Gets the child-index path from the root.</summary>
    public ImmutableArray<int> Path { get; }

    /// <summary>Gets the UTF-16 text offset or row child boundary.</summary>
    public int Offset { get; }

    /// <inheritdoc />
    public bool Equals(MathPosition other)
    {
        return Offset == other.Offset && Path.AsSpan().SequenceEqual(other.Path.AsSpan());
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (int index in Path)
        {
            hash.Add(index);
        }

        hash.Add(Offset);
        return hash.ToHashCode();
    }
}
