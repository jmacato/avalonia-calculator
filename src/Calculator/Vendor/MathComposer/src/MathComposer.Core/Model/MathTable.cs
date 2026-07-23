using System.Collections.Immutable;

namespace MathComposer.Core;

/// <summary>A rectangular table with an explicit layout kind.</summary>
public sealed record MathTable : MathNode
{
    /// <summary>Initializes a validated table.</summary>
    public MathTable(ImmutableArray<ImmutableArray<MathRow>> rows, MathTableKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (rows.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A table requires at least one row.", nameof(rows));
        }

        int columnCount = rows[0].Length;
        if (columnCount == 0)
        {
            throw new ArgumentException("A table requires at least one column.", nameof(rows));
        }

        foreach (ImmutableArray<MathRow> row in rows)
        {
            if (row.IsDefault || row.Length != columnCount || row.Any(static cell => cell is null))
            {
                throw new ArgumentException("Table rows must be non-null and rectangular.", nameof(rows));
            }
        }

        if (kind == MathTableKind.Gathered && columnCount != 1)
        {
            throw new ArgumentException("A gathered table must have exactly one column.", nameof(rows));
        }

        Rows = rows;
        Kind = kind;
    }

    /// <summary>Gets the immutable rectangular cells.</summary>
    public ImmutableArray<ImmutableArray<MathRow>> Rows { get; }

    /// <summary>Gets the table layout kind.</summary>
    public MathTableKind Kind { get; }

    /// <inheritdoc />
    public bool Equals(MathTable? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Kind != other.Kind || Rows.Length != other.Rows.Length)
        {
            return false;
        }

        for (int rowIndex = 0; rowIndex < Rows.Length; rowIndex++)
        {
            if (!Rows[rowIndex].SequenceEqual(other.Rows[rowIndex]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        foreach (ImmutableArray<MathRow> row in Rows)
        {
            foreach (MathRow cell in row)
            {
                hash.Add(cell);
            }
        }

        return hash.ToHashCode();
    }
}
