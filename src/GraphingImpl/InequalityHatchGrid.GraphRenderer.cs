using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Graphing;
using Graphing.Renderer;
using GraphingRaster.Skia;
using JsMath.Port;

namespace GraphingImpl;

internal readonly record struct InequalityHatchGrid(ImmutableArray<ulong> Occupancy, int LatticeIntervals)
{
    public static InequalityHatchGrid Empty { get; } = new(ImmutableArray<ulong>.Empty, 0);
}
