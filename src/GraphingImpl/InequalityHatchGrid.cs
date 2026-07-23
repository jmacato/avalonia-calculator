using System.Collections.Immutable;
using JsMath.Port;

namespace GraphingImpl;

internal readonly record struct InequalityHatchGrid(
    ImmutableArray<ulong> Occupancy,
    int LatticeIntervals,
    SamplingViewport Viewport)
{
    public static InequalityHatchGrid Empty { get; } = new(
        ImmutableArray<ulong>.Empty,
        0,
        default);
}
