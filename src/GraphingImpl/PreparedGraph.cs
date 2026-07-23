using System.Collections.Immutable;
using JsMath.Port;

namespace GraphingImpl;

internal sealed record PreparedGraph(
    GraphSnapshot Snapshot,
    SamplingViewport Viewport,
    double SamplingScale,
    ImmutableArray<PreparedEquationGeometry> Equations,
    bool HasMissingData);
