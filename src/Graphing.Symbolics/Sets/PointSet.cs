using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PointSet(ImmutableArray<ExactReal> Points) : RealSet
{
    public override string Canonical => $"points[{string.Join(',', Points.Select(ExactRealCanonical.Format))}]";
}
