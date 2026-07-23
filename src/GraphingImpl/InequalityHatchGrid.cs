using System.Collections.Immutable;

namespace GraphingImpl;

internal readonly record struct InequalityHatchGrid(ImmutableArray<ulong> Occupancy, int LatticeIntervals)
{
    public static InequalityHatchGrid Empty { get; } = new(ImmutableArray<ulong>.Empty, 0);
}
